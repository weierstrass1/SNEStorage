using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Services;

public partial class ResourceVersionService
{
    public Task<ServiceResultContext> ValidateEdit(
        ResourceVersionEditDTO dto, CancellationToken ct = default)
    {
        if (dto.Version is not null)
        {
            dto.Version = dto.Version.ToLower().Trim();
            if (dto.Version.Length > MAX_VERSION_SIZE || !_version().IsMatch(dto.Version))
                return Task.FromResult(ServiceResult.InvalidVersion.Return(_logger));
        }

        if (dto.ReleaseNotes is not null)
        {
            dto.ReleaseNotes = dto.ReleaseNotes.Trim();
            if (dto.ReleaseNotes.Length < MIN_DESCRIPTION_SIZE ||
                dto.ReleaseNotes.Length > MAX_DESCRIPTION_SIZE)
                return Task.FromResult(ServiceResult.InvalidReleaseNote.Return(_logger));
        }

        return Task.FromResult(ServiceResult.Success.Return(_logger));
    }

    public async Task<OneOf<ResourceVersion, ServiceResultContext>> FindEdit(
        (long ResourceId, long VersionId) key, CancellationToken ct = default)
    {
        var accessResult = await (_accountService.IsModeratorOrAdmin() ?
            _accessService.GetViewAccess(key.ResourceId, ct) :
            _accessService.GetEditAccess(key.ResourceId, ct));

        if (!accessResult.TryPickT0(out _, out var accessError))
            return accessError;

        var resourceQuery = _context.Resources
            .Include(resource => resource.ResourceVersions)
            .Where(resource => resource.Id == key.ResourceId);
        var resourceResult = await _getResource.Execute(
            resourceQuery, ServiceResult.ResourceNotFound, ct: ct);

        if (!resourceResult.TryPickT0(out var resource, out var resourceError))
            return resourceError;

        var version = resource.ResourceVersions
            .FirstOrDefault(version => version.Id == key.VersionId);

        return version is null ?
            ServiceResult.VersionNotFound.Return(_logger) :
            version;
    }

    public async Task<ServiceResultContext> EditInternal(
        ResourceVersion item, ResourceVersionEditDTO dto, CancellationToken ct = default)
    {
        if (item.SealOfQuality && !_accountService.IsModeratorOrAdmin())
            return ServiceResult.VersionHasSealOfQuality.Return(_logger);

        if (dto.Version is not null)
        {
            var validation = await ValidateVersion(item.ResourceId, item.Id, dto.Version, ct);
            if (!validation.TryPickT0(out bool validVersion, out var validationError))
                return validationError;
            if (!validVersion)
                return ServiceResult.InvalidVersion.Return(_logger);
        }

        item.ChangeLog = _dbHelperService.GetEditString(dto.ReleaseNotes, item.ChangeLog);
        item.Version = _dbHelperService.GetEditString(dto.Version, item.Version);
        item.UpdatedAt = DateTime.UtcNow;
        item.AlreadyModerated = false;
        item.SealOfQualityRequested = item.SealOfQuality ||
            (dto.SealOfQualityRequested ?? item.SealOfQualityRequested);

        return await _dbHelperService.TrySyncContext(ct: ct);
    }
}
