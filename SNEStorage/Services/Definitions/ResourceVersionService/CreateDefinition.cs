using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Services;

public partial class ResourceVersionService
{
    public async Task<ServiceResultContext> ValidateCreate(
        (long ResourceId, ResourceVersionCreateDTO Dto) create,
        CancellationToken ct = default)
    {
        var (resourceId, dto) = create;

        if (dto.File.Length > FILE_MAX_SIZE)
            return ServiceResult.FileSizeSurpassTheLimit.Return(_logger);

        string? userId = _accountService.GetUserID();
        if (userId is null)
            return ServiceResult.Forbidden.Return(_logger);

        dto.Version = dto.Version.ToLower().Trim();

        var versionValidation = await ValidateVersion(resourceId, null, dto.Version, ct);
        if (!versionValidation.TryPickT0(out bool validVersion, out var validationError))
            return validationError;
        if (!validVersion)
            return ServiceResult.InvalidVersion.Return(_logger);

        dto.ReleaseNotes = dto.ReleaseNotes.Trim();
        if (dto.ReleaseNotes.Length < MIN_DESCRIPTION_SIZE ||
            dto.ReleaseNotes.Length > MAX_DESCRIPTION_SIZE)
            return ServiceResult.InvalidReleaseNote.Return(_logger);

        var accessResult = await _accessService.GetEditAccess(resourceId, ct);
        return accessResult.TryPickT0(out _, out var accessError) ?
            ServiceResult.Success.Return(_logger) :
            accessError;
    }

    public async Task<OneOf<ResourceVersion, ServiceResultContext>> CreateInternal(
        (long ResourceId, ResourceVersionCreateDTO Dto) create,
        CancellationToken ct = default)
    {
        var (resourceId, dto) = create;
        string userId = _accountService.GetUserID()!;

        var resourceQuery = _context.Resources
            .Include(resource => resource.ResourceVersions)
                .ThenInclude(version => version.File)
            .Where(resource => resource.Id == resourceId);
        var resourceResult = await _getResource.Execute(
            resourceQuery, ServiceResult.ResourceNotFound, ct: ct);

        if (!resourceResult.TryPickT0(out var resource, out var resourceError))
            return resourceError;
        if (resource.ResourceVersions.Any(version => version.Version == dto.Version))
            return ServiceResult.VersionAlreadyExists.Return(_logger);

        var batchResult = await _fileService.BeginBatch(ct);
        if (!batchResult.TryPickT0(out var batchPath, out var batchError))
            return batchError;

        string mainFolder = ResourcePathHelper.GetMainResourceFolder(resource);
        string versionName = GetFullVersion(dto.Version);
        string versionFolder = ResourcePathHelper.SanitizeFolderName(versionName);
        string path = GetVersionPath(dto.File.FileName, versionFolder, mainFolder);
        var fileResult = await _fileService.PrepareFile(
            FileContext.Resource, dto.File, path, batchPath, ct: ct);

        if (!fileResult.TryPickT0(out var file, out var fileError))
            return fileError;

        ResourceVersion version = new()
        {
            Id = 0,
            FileId = 0,
            ResourceId = 0,
            SubmitterUserId = userId,
            Resource = resource,
            CreatedAt = DateTime.UtcNow,
            File = file,
            ChangeLog = dto.ReleaseNotes,
            Version = versionName,
            SealOfQualityRequested = dto.SealOfQualityRequested
        };

        _context.ResourceVersions.Add(version);
        return await _fileService.TrySyncContextWithFiles(batchPath, version, ct);
    }
}
