using System.Data;
using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services.Interfaces;

namespace SNEStorage.Services;

public partial class ResourceService : IDeleteSingle<long, Resource>
{
    public Task<OneOf<Resource, ServiceResultContext>> FindDelete(
        long key, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_accountService.GetUserID()))
            return Task.FromResult<OneOf<Resource, ServiceResultContext>>(
                ServiceResult.Unauthorized.Return(_logger));

        var query = _context.Resources.AsTracking()
            .Where(resource => resource.Id == key);

        return _getResource.Execute(query, ServiceResult.ResourceNotFound, ct: ct);
    }

    public async Task<ServiceResultContext> ValidateDelete(
        Resource item, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_accountService.GetUserID()))
            return ServiceResult.Unauthorized.Return(_logger);

        var accessResult = await _accessService.GetAccess(item.Id, ct);
        if (!accessResult.TryPickT0(out var access, out var accessError))
            return accessError;

        if (_accountService.IsModeratorOrAdmin())
            return ServiceResult.Success.Return(_logger);

        if (!access.ContentAllowed || !access.IsContributor)
            return ServiceResult.Forbidden.Return(_logger);

        var restrictionsResult = await _dbHelperService.TryFunction(
            cancel => _context.Resources.AsNoTracking()
                .Where(resource => resource.Id == item.Id)
                .Select(resource => new
                {
                    HasSeal = resource.ResourceVersions.Any(version => version.SealOfQuality),
                    HasDependents = resource.Dependents.Any(dependency =>
                        dependency.ResourceId != item.Id)
                })
                .FirstOrDefaultAsync(cancel), ct: ct);

        if (!restrictionsResult.TryPickT0(out var restrictions, out var restrictionsError))
            return restrictionsError;
        if (restrictions is null)
            return ServiceResult.ResourceNotFound.Return(_logger);
        if (restrictions.HasSeal)
            return ServiceResult.ResourceHasSealOfQuality.Return(_logger);
        if (restrictions.HasDependents)
            return ServiceResult.ResourceHasDependents.Return(_logger);

        return ServiceResult.Success.Return(_logger);
    }

    public async Task<ServiceResultContext> DeleteInternal(
        Resource item, CancellationToken ct = default)
    {
        var transactionResult = await _dbHelperService.TryFunction(
            cancel => _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancel), ct: ct);

        if (!transactionResult.TryPickT0(out var transaction, out var transactionError))
            return transactionError;

        await using var deletionTransaction = transaction;

        // Recheck inside the transaction so permissions, seals and dependencies
        // cannot change between validation and the delete.
        var validation = await ValidateDelete(item, ct);
        if (validation.Result != ServiceResult.Success)
            return validation;

        var dataResult = await _dbHelperService.TryFunction(async cancel =>
        {
            var comments = await _context.Comments
                .Where(comment => comment.ResourceComment != null &&
                    comment.ResourceComment.ResourceId == item.Id)
                .Include(comment => comment.ResourceComment)
                .ToListAsync(cancel);

            var commentLinks = await _context.CommentComments
                .Where(link =>
                    (link.ParentComment!.ResourceComment != null &&
                    link.ParentComment.ResourceComment.ResourceId == item.Id) ||
                    (link.ChildComment!.ResourceComment != null &&
                    link.ChildComment.ResourceComment.ResourceId == item.Id))
                .ToListAsync(cancel);

            var reports = await _context.Flags
                .Where(flag =>
                    (flag.ResourceFlags != null && flag.ResourceFlags.ResourceId == item.Id) ||
                    (flag.CommentFlags != null &&
                    flag.CommentFlags.Comment!.ResourceComment != null &&
                    flag.CommentFlags.Comment.ResourceComment.ResourceId == item.Id))
                .Include(flag => flag.ResourceFlags)
                .Include(flag => flag.CommentFlags)
                .ToListAsync(cancel);

            var dependencies = await _context.ResourceDependencies
                .Where(dependency => dependency.DependencyResourceId == item.Id)
                .ToListAsync(cancel);

            var fileIds = await _context.Files.AsNoTracking()
                .Where(file =>
                    (file.Resources.Any(resource => resource.Id == item.Id) ||
                    _context.ResourceVersions.Any(version =>
                        version.ResourceId == item.Id && version.FileId == file.Id) ||
                    _context.ResourcePreviews.Any(preview =>
                        preview.ResourceId == item.Id && preview.FileId == file.Id)) &&
                    !file.Resources.Any(resource => resource.Id != item.Id) &&
                    !_context.ResourceVersions.Any(version =>
                        version.ResourceId != item.Id && version.FileId == file.Id) &&
                    !_context.ResourcePreviews.Any(preview =>
                        preview.ResourceId != item.Id && preview.FileId == file.Id) &&
                    !_context.UserInfos.Any(info => info.AvatarId == file.Id))
                .Select(file => file.Id)
                .ToListAsync(cancel);

            return new { Comments = comments, CommentLinks = commentLinks,
                Reports = reports, Dependencies = dependencies, FileIds = fileIds };
        }, ct: ct);

        if (!dataResult.TryPickT0(out var data, out var dataError))
            return dataError;

        var filesResult = await _fileService.PrepareDeletion(data.FileIds, ct);
        if (!filesResult.TryPickT0(out var files, out var filesError))
            return filesError;

        _context.CommentComments.RemoveRange(data.CommentLinks);
        _context.Flags.RemoveRange(data.Reports);
        _context.Comments.RemoveRange(data.Comments);
        _context.ResourceDependencies.RemoveRange(data.Dependencies);
        _context.Resources.Remove(item);

        var updateResult = await _dbHelperService.TryFunction(
            cancel => _context.Resources
                .Where(resource => resource.Id != item.Id &&
                    resource.ResourceDependencies.Any(dependency =>
                        dependency.DependencyResourceId == item.Id))
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(resource => resource.UpdatedAt, DateTime.UtcNow), cancel),
            ct: ct);

        if (!updateResult.TryPickT0(out _, out var updateError))
        {
            _context.ChangeTracker.Clear();
            return updateError;
        }

        var saveResult = await _dbHelperService.TrySyncContext(ct: ct);
        if (saveResult.Result != ServiceResult.Success)
            return saveResult;

        var commitResult = await _dbHelperService.TryFunction(
            cancel => deletionTransaction.CommitAsync(cancel), ct: ct);

        if (commitResult.Result != ServiceResult.Success)
        {
            _context.ChangeTracker.Clear();
            return commitResult;
        }

        // The DB deletion is committed. Physical cleanup is best effort;
        // FinishDeletion logs failures for the existing maintenance workflow.
        _ = _fileService.FinishDeletion(files);
        return ServiceResult.Success.Return(_logger);
    }
}
