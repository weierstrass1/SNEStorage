using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services.QueryComponents;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SNEStorage.Services;

public partial class ResourceVersionService(IWebHostEnvironment environment,
    SnestorageContext context, FileService fileService, AccountService accountService,
    ResourceAccessService accessService, DBHelperService dbHelperService,
    GetFirstOrErrorQueryOperation<Resource> getResource,
    GetFirstOrErrorQueryOperation<Models.File> getFile,
    GetListQueryOperation<ResourceVersion> getVersions,
    GetFirstOrErrorQueryOperation<ResourceVersion> getVersion,
    ILogger<ResourceVersionService> logger)
{
    private const int MIN_DESCRIPTION_SIZE = 10;
    private const int MAX_DESCRIPTION_SIZE = 10000;
    private const int MAX_VERSION_SIZE = 25;
    private const long FILE_MAX_SIZE = 250 * 1024 * 1024;

    private readonly IWebHostEnvironment _environment = environment;
    private readonly SnestorageContext _context = context;
    private readonly FileService _fileService = fileService;
    private readonly AccountService _accountService = accountService;
    private readonly ResourceAccessService _accessService = accessService;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly GetFirstOrErrorQueryOperation<Resource> _getResource = getResource;
    private readonly GetFirstOrErrorQueryOperation<Models.File> _getFile = getFile;
    private readonly GetListQueryOperation<ResourceVersion> _getVersions = getVersions;
    private readonly GetFirstOrErrorQueryOperation<ResourceVersion> _getVersion = getVersion;
    private readonly ILogger<ResourceVersionService> _logger = logger;

    public async Task<OneOf<ResourceVersion, ServiceResultContext>> AddVersion(
        long resourceId, ResourceVersionCreateDTO dto, CancellationToken ct = default)
    {
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
        if (!accessResult.TryPickT0(out _, out var accessError))
            return accessError;

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

    public async Task<ServiceResultContext> EditVersion(long resourceId, long versionId,
        ResourceVersionEditDTO dto, CancellationToken ct = default)
    {
        var accessResult = await _accessService.GetEditAccess(resourceId, ct);
        if (!accessResult.TryPickT0(out _, out var accessError))
            return accessError;

        var resourceQuery = _context.Resources
            .Include(resource => resource.ResourceVersions)
            .Where(resource => resource.Id == resourceId);

        var resourceResult = await _getResource.Execute(
            resourceQuery, ServiceResult.ResourceNotFound, ct: ct);

        if (!resourceResult.TryPickT0(out var resource, out var resourceError))
            return resourceError;

        if (dto.Version is not null)
        {
            dto.Version = dto.Version.ToLower().Trim();

            var validation = await ValidateVersion(resourceId, versionId, dto.Version, ct);
            if (!validation.TryPickT0(out bool validVersion, out var validationError))
                return validationError;

            if (!validVersion)
                return ServiceResult.InvalidVersion.Return(_logger);
        }

        if (dto.ReleaseNotes is not null)
        {
            dto.ReleaseNotes = dto.ReleaseNotes.Trim();

            if (dto.ReleaseNotes.Length < MIN_DESCRIPTION_SIZE ||
                dto.ReleaseNotes.Length > MAX_DESCRIPTION_SIZE)
                return ServiceResult.InvalidReleaseNote.Return(_logger);
        }

        var version = resource.ResourceVersions
            .FirstOrDefault(version => version.Id == versionId);

        if (version is null)
            return ServiceResult.VersionNotFound.Return(_logger);

        version.ChangeLog = _dbHelperService.GetEditString(
            dto.ReleaseNotes, version.ChangeLog);
        version.Version = _dbHelperService.GetEditString(
            dto.Version, version.Version);
        version.UpdatedAt = DateTime.UtcNow;
        version.AlreadyModerated = false;
        version.SealOfQualityRequested = version.SealOfQuality ||
            (dto.SealOfQualityRequested ?? version.SealOfQualityRequested);

        return await _dbHelperService.TrySyncContext(ct: ct);
    }

    public async Task<OneOf<Models.File, ServiceResultContext>> GetFile(
        long resourceId, long resourceFileId, bool prepareDownload = false,
        CancellationToken ct = default)
    {
        var accessResult = await _accessService.GetViewAccess(resourceId, ct);
        if (!accessResult.TryPickT0(out _, out var accessError))
            return accessError;

        IQueryable<Models.File> query = _context.Files
            .AsNoTracking()
            .Where(file =>
                file.Id == resourceFileId &&
                (_context.ResourceVersions.Any(version =>
                    version.ResourceId == resourceId &&
                    version.FileId == file.Id) ||
                _context.ResourcePreviews.Any(preview =>
                    preview.ResourceId == resourceId &&
                    preview.FileId == file.Id)));

        if (prepareDownload)
            query = query.Include(file => file.FileType);

        return await _getFile.Execute(
            query, ServiceResult.FileNotFound, ct: ct);
    }

    public async Task<OneOf<(string, string), ServiceResultContext>> Download(
        long resourceId, long resourceFileId, CancellationToken ct = default)
    {
        var result = await GetFile(resourceId, resourceFileId, true, ct);

        if (!result.TryPickT0(out var file, out var error))
            return error;

        string physicalPath = Path.Combine(_environment.ContentRootPath, file.URL);

        if (!System.IO.File.Exists(physicalPath))
            return ServiceResult.FileNotFound.Return(_logger);

        var updateResult = await _dbHelperService.TryFunction(
            async cancel => await _context.ResourceVersions
                .Where(version => version.ResourceId == resourceId &&
                    version.FileId == resourceFileId)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(version => version.Downloads,
                        version => version.Downloads + 1),
                    cancel),
            ServiceResult.DBError, ct: ct);

        if (updateResult.TryPickT1(out var updateError, out _))
            return updateError;

        var mime = file.FileType?.Value ?? "application/octet-stream";
        return (physicalPath, mime);
    }

    public async Task<OneOf<List<ResourceVersion>, ServiceResultContext>>
        GetResourceAllVersion(long resourceId, CancellationToken ct)
    {
        var accessResult = await _accessService.GetViewAccess(resourceId, ct);
        if (!accessResult.TryPickT0(out _, out var accessError))
            return accessError;

        var query = _context.ResourceVersions
            .AsNoTracking()
            .Where(version => version.ResourceId == resourceId)
            .Include(version => version.File)
            .OrderByDescending(version => version.CreatedAt)
            .ThenByDescending(version => version.Id);

        return await _getVersions.Execute(query, ct: ct);
    }

    public async Task<OneOf<ResourceVersion, ServiceResultContext>>
        GetResourceLastVersion(long resourceId, CancellationToken ct)
    {
        var accessResult = await _accessService.GetViewAccess(resourceId, ct);
        if (!accessResult.TryPickT0(out _, out var accessError))
            return accessError;

        var query = _context.ResourceVersions
            .AsNoTracking()
            .Include(version => version.File)
            .Where(version => version.ResourceId == resourceId)
            .OrderByDescending(version => version.CreatedAt)
            .ThenByDescending(version => version.Id)
            .Take(1);

        return await _getVersion.Execute(
            query, ServiceResult.VersionNotFound, ct: ct);
    }

    internal async Task<ServiceResultContext> ValidateInitialVersion(
        ResourceCreateDTO dto, CancellationToken ct = default)
    {
        dto.Version = dto.Version.ToLower().Trim();

        var validation = await ValidateVersion(null, null, dto.Version, ct);
        if (!validation.TryPickT0(out bool validVersion, out var validationError))
            return validationError;

        return validVersion ?
            ServiceResult.Success.Return(_logger) :
            ServiceResult.InvalidVersion.Return(_logger);
    }

    internal async Task<OneOf<ResourceVersion, ServiceResultContext>>
        PrepareInitialVersion(ResourceCreateDTO dto, string userId,
        string mainFolder, string batchPath, CancellationToken ct = default)
    {
        string version = GetFullVersion(dto.Version);
        string versionFolder = ResourcePathHelper.SanitizeFolderName(version);
        string path = GetVersionPath(dto.File.FileName, versionFolder, mainFolder);

        var fileResult = await _fileService.PrepareFile(
            FileContext.Resource, dto.File, path, batchPath, ct: ct);

        if (!fileResult.TryPickT0(out var file, out var fileError))
            return fileError;

        return new ResourceVersion
        {
            Id = 0,
            ResourceId = 0,
            FileId = 0,
            Version = version,
            ChangeLog = dto.ChangeLog,
            File = file,
            CreatedAt = DateTime.UtcNow,
            SubmitterUserId = userId,
            SealOfQualityRequested = dto.SealOfQualityRequested
        };
    }

    private static string GetFullVersion(string? version)
    {
        return string.IsNullOrWhiteSpace(version) ? "1.0.0" : version.Trim();
    }

    private static string GetVersionPath(
        string filename, string versionFolder, string mainFolder)
    {
        return Path.Combine(mainFolder, $"{Guid.NewGuid():N}_{versionFolder}",
            Path.GetFileName(filename));
    }

    private async Task<OneOf<bool, ServiceResultContext>> ValidateVersion(
        long? resourceId, long? versionId, string version,
        CancellationToken ct = default)
    {
        if (version.Length > MAX_VERSION_SIZE || !_version().IsMatch(version))
            return false;

        if (resourceId == null)
            return true;

        var versionsResult = await _dbHelperService.TryFunction(
            cancel => _context.Resources
                .AsNoTracking()
                .Where(resource => resource.Id == resourceId)
                .Select(resource => resource.ResourceVersions
                    .OrderBy(version => version.CreatedAt)
                    .ThenBy(version => version.Id)
                    .Select(version => new { version.Id, version.Version })
                    .ToList())
                .FirstOrDefaultAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (!versionsResult.TryPickT0(out var versions, out var error))
            return error;

        if (versions == null)
            return false;

        int position = versions.FindIndex(version => version.Id == versionId);

        if (position < 0)
            position = versions.Count;

        var proposed = GetVersionOrder(version);

        for (int i = 0; i < versions.Count; i++)
        {
            if (i == position)
                continue;

            var previous = versions[i].Version?.Trim().ToLowerInvariant();

            if (previous == null || !_version().IsMatch(previous))
                return false;

            int comparison = proposed.CompareTo(GetVersionOrder(previous));

            if (i < position ? comparison <= 0 : comparison >= 0)
                return false;
        }

        return true;
    }

    private static (int Major, int Minor, int Patch, int Stage, int Revision)
        GetVersionOrder(string version)
    {
        var parts = version.Split('-', 2);
        var numbers = parts[0].Split('.');
        string[] suffix = parts.Length == 2 ? parts[1].Split('.', 2) : [];

        int stage = suffix.ElementAtOrDefault(0) switch
        {
            "alpha" => 0,
            "beta" => 1,
            "rc" => 2,
            _ => 3
        };

        return (int.Parse(numbers[0], CultureInfo.InvariantCulture),
            int.Parse(numbers.ElementAtOrDefault(1) ?? "0",
                CultureInfo.InvariantCulture),
            int.Parse(numbers.ElementAtOrDefault(2) ?? "0",
                CultureInfo.InvariantCulture),
            stage,
            suffix.Length == 2 ?
                int.Parse(suffix[1], CultureInfo.InvariantCulture) :
                -1);
    }

    [GeneratedRegex(@"\A[0-9]{1,4}(?:\.[0-9]{1,4}){0,2}(?:-(?:alpha|beta|rc)(?:\.[0-9]{1,4})?)?\z")]
    private static partial Regex _version();
}
