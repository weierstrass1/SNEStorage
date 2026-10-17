using System.Data;
using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Services;

public partial class ResourceService
{
    private async Task<OneOf<Resource, ServiceResultContext>> SaveResourceChanges(
        Resource resource, string batchPath, CancellationToken ct = default)
    {
        var transactionResult = await _dbHelperService.TryFunction(
            cancel => _context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable, cancel), ct: ct);

        if (!transactionResult.TryPickT0(out var transaction, out var transactionError))
        {
            _ = await _fileService.BatchRollback(batchPath, CancellationToken.None);
            return transactionError;
        }

        await using var transactionScope = transaction;
        bool commitAttempted = false;

        var result = await ValidateResourceSave(resource, ct);

        if (result.Result == ServiceResult.Success)
            result = await _dbHelperService.TrySyncContext(ct: ct);

        if (result.Result == ServiceResult.Success)
        {
            commitAttempted = true;
            result = await _dbHelperService.TryFunction(
                cancel => transaction.CommitAsync(cancel), ct: ct);
        }

        if (result.Result != ServiceResult.Success)
        {
            _ = await _dbHelperService.TryFunction(
                cancel => transaction.RollbackAsync(cancel), ct: CancellationToken.None);
            _context.ChangeTracker.Clear();

            // A failed commit response does not prove that SQL Server rolled back.
            if (!commitAttempted)
                _ = await _fileService.BatchRollback(batchPath, CancellationToken.None);
            return result;
        }

        _ = _fileService.BatchComplete(batchPath);
        return resource;
    }

    private async Task<ServiceResultContext> ValidateResourceSave(
        Resource resource, CancellationToken ct = default)
    {
        if (resource.Id > 0)
        {
            var currentResult = await _dbHelperService.TryFunction(
                cancel => _context.Resources.AsNoTracking()
                    .Where(current => current.Id == resource.Id)
                    .Select(current => new
                    {
                        current.VisibilityId,
                        HasSeal = current.ResourceVersions.Any(version => version.SealOfQuality)
                    })
                    .FirstOrDefaultAsync(cancel), ct: ct);

            if (!currentResult.TryPickT0(out var current, out var currentError))
                return currentError;
            if (current is null)
                return ServiceResult.ResourceNotFound.Return(_logger);

            var visibilityResult = _dbHelperService.TryFunction(() =>
            {
                _context.ChangeTracker.DetectChanges();
                return _context.Entry(resource).Property(item => item.VisibilityId).IsModified;
            });

            if (!visibilityResult.TryPickT0(out bool visibilityChanged, out var visibilityError))
                return visibilityError;

            long visibilityId = visibilityChanged ? resource.VisibilityId : current.VisibilityId;

            if (current.HasSeal && (!_accountService.IsModeratorOrAdmin() || visibilityId != 1))
                return ServiceResult.ResourceHasSealOfQuality.Return(_logger);

            if (visibilityId == 3)
            {
                var dependentsResult = await _dbHelperService.TryFunction(
                    cancel => _context.ResourceDependencies.AsNoTracking()
                        .AnyAsync(dependency => dependency.DependencyResourceId == resource.Id &&
                            dependency.ResourceId != resource.Id, cancel), ct: ct);

                if (!dependentsResult.TryPickT0(out bool hasDependents, out var dependentsError))
                    return dependentsError;
                if (hasDependents)
                    return ServiceResult.ResourceHasDependents.Return(_logger);
            }
        }

        long[] dependencyIds = [.. resource.ResourceDependencies
            .Select(dependency => dependency.DependencyResourceId)
            .Distinct()
            .OrderBy(id => id)];

        if (dependencyIds.Length > 0)
        {
            var dependenciesResult = await _dbHelperService.TryFunction(
                cancel => _context.Resources.AsNoTracking()
                    .Where(dependency => dependencyIds.Contains(dependency.Id) &&
                        dependency.VisibilityId != 3)
                    .CountAsync(cancel), ct: ct);

            if (!dependenciesResult.TryPickT0(out int availableCount, out var dependenciesError))
                return dependenciesError;
            if (availableCount != dependencyIds.Length)
                return ServiceResult.ResourceNotFound.Return(_logger);
        }

        return ServiceResult.Success.Return(_logger);
    }
}
