using System.Data;
using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Services;

public partial class ResourceVersionService
{
    public async Task<OneOf<ResourceVersion, ServiceResultContext>> FindDelete(
        (long ResourceId, long VersionId) key, CancellationToken ct = default)
    {
        var accessResult = await (_accountService.IsModeratorOrAdmin() ?
            _accessService.GetViewAccess(key.ResourceId, ct) :
            _accessService.GetEditAccess(key.ResourceId, ct));
        if (!accessResult.TryPickT0(out _, out var accessError))
            return accessError;

        var query = _context.ResourceVersions.AsTracking()
            .Where(version => version.ResourceId == key.ResourceId &&
                version.Id == key.VersionId);

        return await _getVersion.Execute(query, ServiceResult.VersionNotFound, ct: ct);
    }

    public async Task<ServiceResultContext> ValidateDelete(
        ResourceVersion item, CancellationToken ct = default)
    {
        var accessResult = await (_accountService.IsModeratorOrAdmin() ?
            _accessService.GetViewAccess(item.ResourceId, ct) :
            _accessService.GetEditAccess(item.ResourceId, ct));
        if (!accessResult.TryPickT0(out _, out var accessError))
            return accessError;

        if (item.SealOfQuality && !_accountService.IsModeratorOrAdmin())
            return ServiceResult.VersionHasSealOfQuality.Return(_logger);

        var countResult = await _dbHelperService.TryFunction(
            cancel => _context.ResourceVersions.AsNoTracking()
                .CountAsync(version => version.ResourceId == item.ResourceId, cancel), ct: ct);

        if (!countResult.TryPickT0(out int versionCount, out var countError))
            return countError;

        return versionCount > 1 ?
            ServiceResult.Success.Return(_logger) :
            ServiceResult.CantDeleteOnlyVersion.Return(_logger);
    }

    public async Task<ServiceResultContext> DeleteInternal(
        ResourceVersion item, CancellationToken ct = default)
    {
        var transactionResult = await _dbHelperService.TryFunction(
            cancel => _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancel), ct: ct);

        if (!transactionResult.TryPickT0(out var transaction, out var transactionError))
            return transactionError;

        await using var deletionTransaction = transaction;

        var reloadResult = await _dbHelperService.TryFunction(
            cancel => _context.Entry(item).ReloadAsync(cancel), ct: ct);
        if (reloadResult.Result != ServiceResult.Success)
            return reloadResult;
        if (_context.Entry(item).State == EntityState.Detached)
            return ServiceResult.VersionNotFound.Return(_logger);

        var validation = await ValidateDelete(item, ct);
        if (validation.Result != ServiceResult.Success)
            return validation;

        var fileIdsResult = await _dbHelperService.TryFunction(
            cancel => _context.Files.AsNoTracking()
                .Where(file => file.Id == item.FileId &&
                    !_context.ResourceVersions.Any(version =>
                        version.Id != item.Id && version.FileId == file.Id) &&
                    !_context.ResourcePreviews.Any(preview => preview.FileId == file.Id) &&
                    !file.Resources.Any() &&
                    !_context.UserInfos.Any(info => info.AvatarId == file.Id))
                .Select(file => file.Id)
                .ToListAsync(cancel), ct: ct);

        if (!fileIdsResult.TryPickT0(out var fileIds, out var fileIdsError))
            return fileIdsError;

        var filesResult = await _fileService.PrepareDeletion(fileIds, ct);
        if (!filesResult.TryPickT0(out var files, out var filesError))
            return filesError;

        _context.ResourceVersions.Remove(item);

        var updateResult = await _dbHelperService.TryFunction(
            cancel => _context.Resources
                .Where(resource => resource.Id == item.ResourceId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(resource => resource.UpdatedAt, DateTime.UtcNow), cancel), ct: ct);

        if (!updateResult.TryPickT0(out _, out var updateError))
        {
            _context.ChangeTracker.Clear();
            return updateError;
        }

        var saveResult = await _dbHelperService.TrySyncContext(ct: ct);
        if (saveResult.Result != ServiceResult.Success)
        {
            _context.ChangeTracker.Clear();
            return saveResult;
        }

        var commitResult = await _dbHelperService.TryFunction(
            cancel => deletionTransaction.CommitAsync(cancel), ct: ct);

        if (commitResult.Result != ServiceResult.Success)
        {
            _context.ChangeTracker.Clear();
            return commitResult;
        }

        _ = _fileService.FinishDeletion(files);
        return ServiceResult.Success.Return(_logger);
    }
}
