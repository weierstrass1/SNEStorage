using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;
using System.Text.RegularExpressions;

namespace SNEStorage.Services;

public enum ResourceServiceResult
{
    Success,
    CantAddScoreToPrivateResource,
    InvalidScore,
    EmptyComment,
    CantScoreSelfResource,
    PreviewAmountSurpassTheLimit,
    PreviewSizeSurpassTheLimit,
    FileSizeSurpassTheLimit,
    EmptyName,
    EmptyDescription,
    EmptyTypeList,
    ResourceNotFound,
    UserNotFound,
    Unauthorized,
    FileCreationError,
    BatchCreationError,
    FileNotFound,
    VersionNotFound,
    VersionAlreadyExists,
    ResourceTypeNotFound,
    FileDeletionError,
    DBError
}
public partial class ResourceService(IWebHostEnvironment environment,
    SnestorageContext context,
    FileService fileService,
    AccountService accountService,
    DBHelperService dbHelperService)
{
    public const long FILE_MAX_SIZE = 250 * 1024 * 1024;
    public const long PREVIEW_MAX_SIZE = 5 * 1024 * 1024;
    public const int MAX_AMOUNT_OF_PREVIEWS = 20;
    private readonly IWebHostEnvironment _environment = environment;
    private readonly SnestorageContext _context = context;
    private readonly FileService _fileService = fileService;
    private readonly AccountService _accountService = accountService;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    public async Task<List<Resource>> GetResourceLike(string likeString, int maxAmount = 10, CancellationToken ct = default)
    {
        string? userId = _accountService.GetUserID();
        var term = likeString?.ToLower().Trim() ?? "";
        var query = _context.Resources
            .AsNoTracking()
            .Where(r => r.VisibilityId != 3 ||
                (userId != null && (r.SubmitterId == userId || r.ResourcesAuthors.Any(a => a.UserId == userId))));

        if (!string.IsNullOrWhiteSpace(term) && term.Trim().Length >= 2)
        {
            var escaped = term
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_")
                .Replace("[", @"\[");

            var pattern = $"%{escaped}%";

            query = query.Where(v => EF.Functions.Like(v.Name, pattern, @"\"));
        }

        return await query
            .OrderBy(v => v.Name)
            .Take(maxAmount)
            .ToListAsync(ct);
    }
    public async Task<OneOf<ResourceComments,ResourceServiceResult>> AddComment(long resourceId, string commentText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(commentText))
            return ResourceServiceResult.EmptyComment;
        string? userId = _accountService.GetUserID();
        if (userId == null)
            return ResourceServiceResult.Unauthorized;
        var resUser = await _dbHelperService.TryFunction(
            async cancel => await _context.Users
                .Include(u => u.UserInfo)
                    .ThenInclude(u=> u!.Avatar)
                .FirstOrDefaultAsync(r => r.Id == userId, cancel),
            ResourceServiceResult.DBError, ct: ct);
        if (!resUser.TryPickT0(out var user, out var error))
            return error;
        if (user == null)
            return ResourceServiceResult.Unauthorized;

        var res = await _dbHelperService.TryFunction(
                    async cancel => await _context.Resources
                        .Include(r => r.ResourcesAuthors)
                        .Include(r => r.ResourceVersions)
                        .FirstOrDefaultAsync(r => r.Id == resourceId, cancel),
                    ResourceServiceResult.DBError, ct: ct);
        if (!res.TryPickT0(out var resource, out error))
            return error;
        if (resource == null)
            return ResourceServiceResult.ResourceNotFound;

        if (resource.VisibilityId == 3 &&
            !_accountService.IsModeratorOrAdmin() &&
            resource.SubmitterId != userId &&
            !resource.ResourcesAuthors.Any(ra => ra.UserId == userId) &&
            !resource.ResourceVersions.Any(r => r.SubmitterUserId == userId))
            return ResourceServiceResult.Unauthorized;

        ResourceComments rc = new()
        {
            Id = 0,
            ResourceId = resourceId,
            Comment = new()
            {
                Id = 0,
                Score = new Score(),
                UserId = userId,
                User = user,
                Text = commentText,
                PublishDate = DateTime.UtcNow
            }
        };

        rc.Comment.UserSummary = UserNameDTO.FromApplicationUser(user);

        _context.ResourcesComments.Add(rc);

        return await _dbHelperService.TrySyncContext(rc,
            ResourceServiceResult.DBError, ct);
    }
    public async Task<OneOf<ScoreUsers, ResourceServiceResult>> AddOrEditScore(long resourceId, long value, CancellationToken ct = default)
    {
        if (value is < 1 or > 5)
            return ResourceServiceResult.InvalidScore;
        string? userId = _accountService.GetUserID();
        if (userId == null)
            return ResourceServiceResult.Unauthorized;

        var res = await _dbHelperService.TryFunction(
            async cancel => await _context.Resources
                .Include(r => r.ResourcesAuthors)
                .Include(r => r.ResourceVersions)
                .Include(r => r.Score)
                    .ThenInclude(s => s!.ScoresUsers)
                .FirstOrDefaultAsync(r => r.Id == resourceId, cancel),
            ResourceServiceResult.DBError, ct: ct);
        if (!res.TryPickT0(out var resource, out var error))
            return error;
        if (resource == null)
            return ResourceServiceResult.ResourceNotFound;

        if (resource.VisibilityId == 3)
            return ResourceServiceResult.CantAddScoreToPrivateResource;

        if (resource.SubmitterId == userId ||
            resource.ResourceVersions.Any(v => v.SubmitterUserId == userId) ||
            resource.ResourcesAuthors.Any(v => v.UserId == userId))
            return ResourceServiceResult.CantScoreSelfResource;

        var scoreUsers = resource.Score!.ScoresUsers.FirstOrDefault(s => s.UserId == userId);
        if(scoreUsers == null)
        {
            scoreUsers = new()
            {
                Id = 0,
                UserId = userId,
                ScoreId = resource.ScoreId,
            };
            _context.ScoresUsers.Add(scoreUsers);
        }
        scoreUsers.Value = value;
        return await _dbHelperService.TrySyncContext(scoreUsers, 
            ResourceServiceResult.DBError, ct);
    }
    public async Task<ResourceServiceResult> Edit(long resourceId, ResourceEditDTO dto, CancellationToken ct = default)
    {
        if (dto.Name != null &&
            (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length < 2))
            return ResourceServiceResult.EmptyName;
        if (dto.Description != null && (
            string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Trim().Length < 10))
            return ResourceServiceResult.EmptyDescription;
        if (dto.Previews.Any(pr => pr.Length > PREVIEW_MAX_SIZE))
            return ResourceServiceResult.PreviewSizeSurpassTheLimit;
        var res = await Get(resourceId, true, ct);
        var check = CheckResourceIsEditable(res!);
        if (check != ResourceServiceResult.Success)
            return check;
        var resource = res.AsT0!;

        var userId = _accountService.GetUserID();
        if (string.IsNullOrWhiteSpace(userId))
            return ResourceServiceResult.UserNotFound;

        var name = SanitizeFolderName(dto.Name);

        var mainFolder = GetMainResourceFolder(resource);
        string? batchfile = await _fileService.BeginBatch(ct);
        if (batchfile == null)
            return ResourceServiceResult.BatchCreationError;

        var prevResult = await GetPreviews(dto.Previews, mainFolder, batchfile, ct);
        if (prevResult.IsT1)
            return prevResult.AsT1;

        List<ResourcePreview> resourcePreviews = prevResult.AsT0;

        var tagRes = await _dbHelperService.TryFunction(
            async cancel => await GetTags(dto.Tags, cancel),
            ResourceServiceResult.DBError,
            async () => await _fileService.BatchRollback(batchfile, ct), ct);
        if (!tagRes.TryPickT0(out var tags, out var tagError))
            return tagError;

        var typesRes = await _dbHelperService.TryFunction(
            async cancel => await GetTypes(dto.Types, cancel),
            ResourceServiceResult.DBError,
            async () => await _fileService.BatchRollback(batchfile, ct), ct);
        if (!typesRes.TryPickT0(out var types, out var typeError))
            return typeError;

        if (types.Any(t => t == null))
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ResourceServiceResult.ResourceTypeNotFound;
        }

        var authorRes = await _dbHelperService.TryFunction(
            async cancel => await GetAuthors(dto.AuthorsIDs, true, userId, ct),
            ResourceServiceResult.DBError,
            async () => await _fileService.BatchRollback(batchfile, ct), ct);
        if (!authorRes.TryPickT0(out var authors, out var authorError))
            return authorError;

        if (authors.Any(t => t == null))
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ResourceServiceResult.UserNotFound;
        }

        List<ResourceExternalAuthor> externalAuthors = GetExternalAuthors(dto.ExternalAuthors);

        var dependenciesRes = await _dbHelperService.TryFunction(
            async cancel => await GetDependencies(dto.DependencyIDs, ct),
            ResourceServiceResult.DBError,
            async () => await _fileService.BatchRollback(batchfile, ct), ct);
        if (!dependenciesRes.TryPickT0(out var dependencies, out var dependenciesError))
            return dependenciesError;

        if (dependencies.Any(t => t == null))
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ResourceServiceResult.ResourceNotFound;
        }

        List<ResourceExternalDependency> externalDependencies = GetExternalDependencies(dto.ExternalDependencies);

        resource.ResourceResourceTypes = [..resource.ResourceResourceTypes
            .Where(d => !dto.RemovedTypeIDs.Contains(d.Id))
            .Concat(types)
            .DistinctBy(d =>d!.ResourceTypeId)!];
        if (resource.ResourceResourceTypes.Count <= 0)
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ResourceServiceResult.EmptyTypeList;
        }

        resource.ResourcePreviews = [..resource.ResourcePreviews
            .Where(d => !dto.RemovePreviewIDs.Contains(d.Id))
            .Concat(resourcePreviews.Where(t => t.FileId != 0))
            .DistinctBy(d =>d.FileId)
            .Concat(resourcePreviews.Where(t => t.FileId == 0))];

        if (resource.ResourcePreviews.Count > MAX_AMOUNT_OF_PREVIEWS)
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ResourceServiceResult.PreviewAmountSurpassTheLimit;
        }

        resource.Name = dto.Name?.Trim() ?? resource.Name;
        resource.Description = dto.Description?.Trim() ?? resource.Description;
        resource.UpdatedAt = DateTime.UtcNow;
        resource.AlreadyModerated = false;
        resource.ResourceDependencies = [..resource.ResourceDependencies
            .Where(d => !dto.RemoveDependencyIDs.Contains(d.Id))
            .Concat(dependencies)
            .DistinctBy(d =>d!.DependencyResourceId)!];
        resource.ResourceExternalDependencies = [..resource.ResourceExternalDependencies
            .Where(d => !dto.RemoveExternalDependencyIDs.Contains(d.Id))
            .Concat(externalDependencies.Where(t => t.Id != 0))
            .DistinctBy(d => d.Id)
            .Concat(externalDependencies.Where(t => t.Id == 0))];
        resource.ResourcesAuthors = [..resource.ResourcesAuthors
            .Where(d => !dto.RemoveAuthorsIDs.Contains(d.Id))
            .Concat(authors)
            .DistinctBy(d =>d!.UserId)!];
        resource.ResourceExternalAuthors = [..resource.ResourceExternalAuthors
            .Where(d => !dto.RemoveExternalAuthorsIDs.Contains(d.Id))
            .Concat(externalAuthors.Where(t => t.Id != 0))
            .DistinctBy(d => d.Id)
            .Concat(externalAuthors.Where(t => t.Id == 0))];
        resource.ResourceTags = [..resource.ResourceTags
            .Where(d => !dto.RemoveTagIDs.Contains(d.Id))
            .Concat(tags.Where(t => t.TagId != 0))
            .DistinctBy(d => d.TagId)
            .Concat(tags.Where(t => t.TagId == 0))];
        resource.RepositoryURL = _dbHelperService.GetEditString(dto.RepositoryURL, resource.RepositoryURL);
        resource.VideogameId = dto.VideogameId ?? resource.VideogameId;
        resource.VisibilityId = dto.VisibilityId ?? resource.VisibilityId;
        resource.IncludesPorn = dto.IncludesPorn ?? resource.IncludesPorn;
        resource.IncludesGore = dto.IncludesGore ?? resource.IncludesGore;
        resource.IncludesPolitics = dto.IncludesPolitics ?? resource.IncludesPolitics;
        resource.IncludesSlurs = dto.IncludesSlurs ?? resource.IncludesSlurs;
        resource.IncludesSensitiveContent = dto.IncludesSensitiveContent ?? resource.IncludesSensitiveContent;
        long[] prIds = [.. dto.RemovePreviewIDs
            .Where(id => !resource.ResourcePreviews.Any(pr => pr.Id == id))];

        var removePrs = await _dbHelperService.TryFunction(
                            async cancel => await _context.ResourcePreviews
                                .AsNoTracking()
                                .Where(pr => prIds.Contains(pr.Id))
                                .ToListAsync(cancel),
                            async () => await _fileService.BatchRollback(batchfile, ct), ct);
        if(removePrs == null)
            return ResourceServiceResult.DBError;

        var files = await _dbHelperService.TryFunction(
            async cancel => await _fileService
                .PrepareDeletion(removePrs.Select(pr => pr.Id), cancel),
            async () => await _fileService.BatchRollback(batchfile, ct), ct);
        if(files == null)
            return ResourceServiceResult.DBError;

        if (!await _dbHelperService.TrySyncContextWithFiles(batchfile, ct)) 
            return ResourceServiceResult.DBError;

        _fileService.FinishDeletion(files);
        return ResourceServiceResult.Success;
    }
    public async Task<OneOf<ResourceVersion, ResourceServiceResult>> AddVersion(long resourceId, ResourceVersionCreateDTO dto, CancellationToken ct = default)
    {
        if (dto.File.Length > FILE_MAX_SIZE)
            return ResourceServiceResult.FileSizeSurpassTheLimit;
        string? userId = _accountService.GetUserID();
        if (userId == null)
            return ResourceServiceResult.Unauthorized;

        var res = await _dbHelperService.TryFunction(async cancel => await _context.Resources
            .Include(r => r.ResourceVersions)
                .ThenInclude(r => r.File)
            .Include(r => r.ResourcesAuthors)
            .FirstOrDefaultAsync(r => r.Id == resourceId, cancel),
            ResourceServiceResult.DBError, ct: ct);
        var check = CheckResourceIsEditable(res);
        if (check != ResourceServiceResult.Success)
            return check;

        var resource = res.AsT0!;
        if (resource.ResourceVersions.Any(v => v.Version == dto.Version))
            return ResourceServiceResult.VersionAlreadyExists;

        string? batchPath = await _fileService.BeginBatch(ct);
        if (batchPath == null)
            return ResourceServiceResult.BatchCreationError;

        string mainFolder = GetMainResourceFolder(resource);
        string versionName = GetFullVersion(dto.Version);
        string versionFolder = SanitizeFolderName(versionName);
        string path = GetVersionPath(dto.File.FileName, versionFolder, mainFolder);

        var file = await _fileService.PrepareFile(dto.File, path, batchPath, ct);
        if(file == null)
        {
            await _fileService.BatchRollback(batchPath, ct);
            return ResourceServiceResult.FileCreationError;
        }

        ResourceVersion version = new()
        {
            Id = 0,
            FileId = 0,
            ResourceId = 0,
            SubmitterUserId = userId,
            Resource = resource,
            File = file,
            ChangeLog = dto.ReleaseNotes,
            Version = versionName,
            SealOfQualityRequested = dto.SealOfQualityRequested
        };

        _context.ResourceVersions.Add(version);

        var result = await _dbHelperService.TryFunction(
            async cancel => (int?)await _context.SaveChangesAsync(cancel),
            ct: ct);
        if (result != null)
        {
            _fileService.BatchComplete(batchPath);
            return version;
        }
        await _fileService.BatchRollback(batchPath, ct);
        return ResourceServiceResult.DBError;
    }
    public async Task<ResourceServiceResult> EditVersion(long resourceId, long versionId, ResourceVersionEditDTO dto, CancellationToken ct = default)
    {
        var res = await _dbHelperService.TryFunction(async cancel => await _context.Resources
            .Include(r => r.ResourceVersions)
            .Include(r => r.ResourcesAuthors)
            .FirstOrDefaultAsync(r => r.Id == resourceId, cancel),
            ResourceServiceResult.DBError, ct: ct);
        var check = CheckResourceIsEditable(res);
        if (check != ResourceServiceResult.Success)
            return check;

        var version = res.AsT0!.ResourceVersions.FirstOrDefault(v => v.Id == versionId);
        if (version == null)
            return ResourceServiceResult.VersionNotFound;
        version.ChangeLog = _dbHelperService.GetEditString(dto.ReleaseNotes, version.ChangeLog);
        version.Version = _dbHelperService.GetEditString(dto.Version, version.Version);
        version.UpdatedAt = DateTime.UtcNow;
        version.AlreadyModerated = false;
        version.SealOfQualityRequested = version.SealOfQuality || 
            (dto.SealOfQualityRequested ?? version.SealOfQualityRequested);

        var result = await _dbHelperService.TryFunction(
            async cancel => (int?) await _context.SaveChangesAsync(cancel),
            ct: ct);
        return result == null ?
            ResourceServiceResult.DBError :
            ResourceServiceResult.Success;
    }
    public async Task<OneOf<Resource, ResourceServiceResult>> Get(long resourceId, bool tracking = false, CancellationToken ct = default)
    {
        var res = await _dbHelperService.TryFunction(
            async cancel => {
                IQueryable<Resource> query = _context.Resources;
                if (!tracking)
                    query = query.AsNoTracking();
                return await query
                .AsSplitQuery()
                .Include(r => r.Videogame)
                .Include(r => r.Visibility)
                .Include(r => r.Submitter)
                .Include(r => r.ResourceResourceTypes)
                    .ThenInclude(r => r.ResourceType)
                    .ThenInclude(r => r!.Category)
                .Include(r => r.Score)
                    .ThenInclude(r => r!.ScoresUsers)
                .Include(r => r.ResourceVersions)
                    .ThenInclude(version => version.File)
                .Include(r => r.ResourceVersions)
                    .ThenInclude(version => version.SubmitterUser)
                .Include(r => r.ResourcePreviews)
                    .ThenInclude(preview => preview.File)
                .Include(r => r.ResourceTags)
                    .ThenInclude(resourceTag => resourceTag.Tag)
                .Include(r => r.ResourcesAuthors)
                    .ThenInclude(r => r.User)
                    .ThenInclude(r => r!.UserInfo)
                .Include(r => r.ResourceDependencies)
                    .ThenInclude(r => r.DependencyResource)
                .Include(r => r.ResourceExternalAuthors)
                .Include(r => r.ResourceExternalDependencies)
                .FirstOrDefaultAsync(r => r.Id == resourceId, cancel);
            },
            ResourceServiceResult.DBError, ct: ct);
        var result = CheckResourceViability(res);
        if (result != ResourceServiceResult.Success)
            return result;
        var resource = res.AsT0!;

        UserNameDTO.FillUserSummaries(
            (target, summary) => target.UserSummary = summary, 
            () => resource.ResourcesAuthors.Select(ra => (ra, ra.User))!);
        UserNameDTO.FillUserSummaries(
            (target, summary) => target.UserSummary = summary,
            () => resource.ResourceVersions.Select(ra => (ra, ra.SubmitterUser))!);
        resource.UserSummary = UserNameDTO.FromApplicationUser(resource.Submitter!);

        return resource;
    }
    public async Task<OneOf<Models.File, ResourceServiceResult>> GetFile(long resourceId, long resourceFileId, bool prepareDownload = false, CancellationToken ct = default)
    {
        var res = !prepareDownload ?
            await _dbHelperService.TryFunction(async cancel => await _context.Resources
                .AsNoTracking()
                .Include(r => r.ResourceVersions)
                    .ThenInclude(version => version.File)
                .Include(r => r.ResourcePreviews)
                    .ThenInclude(preview => preview.File)
                .Include(r => r.ResourcesAuthors)
                .FirstOrDefaultAsync(r => r.Id == resourceId, cancel),
                ResourceServiceResult.DBError, ct: ct) :
            await _dbHelperService.TryFunction(async cancel => await _context.Resources
                .AsNoTracking()
                .Include(r => r.ResourceVersions)
                    .ThenInclude(version => version.File)
                    .ThenInclude(file => file!.FileType)
                .Include(r => r.ResourcePreviews)
                    .ThenInclude(preview => preview.File)
                    .ThenInclude(file => file!.FileType)
                .Include(r => r.ResourcesAuthors)
                .FirstOrDefaultAsync(r => r.Id == resourceId, cancel),
                ResourceServiceResult.DBError, ct: ct);

        var result = CheckResourceViability(res);
        if (result != ResourceServiceResult.Success)
            return result;

        var resource = res.AsT0!;
        var file = resource.ResourceVersions
            .FirstOrDefault(pr => pr.FileId == resourceFileId)?.File;

        if (file != null)
            return file;
        file = resource.ResourcePreviews
            .FirstOrDefault(pr => pr.FileId == resourceFileId)?.File;
        return file != null ?
            file :
            ResourceServiceResult.FileNotFound;
    }
    public async Task<OneOf<(string,string), ResourceServiceResult>> Download(long resourceId, long resourceFileId, CancellationToken ct = default)
    {
        var res = await GetFile(resourceId, resourceFileId, true, ct);
        if (!res.TryPickT0(out var file, out var error))
            return error;
        var physicalPath = Path.Combine(_environment.ContentRootPath, file.URL);

        if (!System.IO.File.Exists(physicalPath))
            return ResourceServiceResult.FileNotFound;

        var mime = file.FileType?.Value ?? "application/octet-stream";

        return (physicalPath, mime);
    }
    public async Task<OneOf<Resource, ResourceServiceResult>> Create(ResourceCreateDTO dto, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.Name) || dto.Name.Trim().Length < 2)
            return ResourceServiceResult.EmptyName;
        if (string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Trim().Length < 10)
            return ResourceServiceResult.EmptyDescription;
        if (dto.File.Length > FILE_MAX_SIZE)
            return ResourceServiceResult.FileSizeSurpassTheLimit;
        if (dto.Previews.Any(pr => pr.Length > PREVIEW_MAX_SIZE))
            return ResourceServiceResult.PreviewSizeSurpassTheLimit;
        if (dto.Previews.Length > MAX_AMOUNT_OF_PREVIEWS)
            return ResourceServiceResult.PreviewAmountSurpassTheLimit;
        if (dto.Types is null || dto.Types.Length == 0)
            return ResourceServiceResult.EmptyTypeList;

        var userId = _accountService.GetUserID();
        if (string.IsNullOrWhiteSpace(userId))
            return ResourceServiceResult.UserNotFound;

        var name = SanitizeFolderName(dto.Name);
        string version = GetFullVersion(dto.Version);
        string versionFolder = SanitizeFolderName(version);

        var mainFolder = Path.Combine("Storage", "Resources",
            $"{Guid.NewGuid():N}_{name}");
        string? batchfile = await _fileService.BeginBatch(ct);
        if (batchfile == null)
            return ResourceServiceResult.BatchCreationError;

        var mainFileRes = await GetMainFile(dto, versionFolder, mainFolder, batchfile, ct);
        if (mainFileRes.IsT1)
            return mainFileRes.AsT1;

        Models.File? file = mainFileRes.AsT0;

        var prevResult = await GetPreviews(dto.Previews, mainFolder, batchfile, ct);
        if (prevResult.IsT1)
            return prevResult.AsT1;

        List<ResourcePreview> resourcePreviews = prevResult.AsT0;
        ResourceVersion resourceVersion = GetVersion(dto, userId, version, file);

        var tagRes = await _dbHelperService.TryFunction(
            async cancel => await GetTags(dto.Tags, cancel),
            ResourceServiceResult.DBError,
            async () => await _fileService.BatchRollback(batchfile, ct), ct);
        if (!tagRes.TryPickT0(out var tags, out var tagError))
            return tagError;

        var typesRes = await _dbHelperService.TryFunction(
            async cancel => await GetTypes(dto.Types, cancel),
            ResourceServiceResult.DBError,
            async () => await _fileService.BatchRollback(batchfile, ct), ct);
        if (!typesRes.TryPickT0(out var types, out var typeError))
            return typeError;

        if (types.Any(t => t == null))
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ResourceServiceResult.ResourceTypeNotFound;
        }

        var authorRes = await _dbHelperService.TryFunction(
            async cancel => await GetAuthors(dto.AuthorsIDs, dto.IAmAuthor, userId, ct),
            ResourceServiceResult.DBError,
            async () => await _fileService.BatchRollback(batchfile, ct), ct);
        if (!authorRes.TryPickT0(out var authors, out var authorError))
            return authorError;

        if (authors.Any(t => t == null))
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ResourceServiceResult.UserNotFound;
        }

        List<ResourceExternalAuthor> externalAuthors = GetExternalAuthors(dto.ExternalAuthors);

        var dependenciesRes = await _dbHelperService.TryFunction(
            async cancel => await GetDependencies(dto.DependencyIDs, ct),
            ResourceServiceResult.DBError,
            async () => await _fileService.BatchRollback(batchfile, ct), ct);
        if (!dependenciesRes.TryPickT0(out var dependencies, out var dependenciesError))
            return dependenciesError;

        if (dependencies.Any(t => t == null))
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ResourceServiceResult.ResourceNotFound;
        }

        List<ResourceExternalDependency> externalDependencies = GetExternalDependencies(dto.ExternalDependencies);

        Resource res = new()
        {
            Id = 0,
            SubmitterId = userId,
            Name = dto.Name,
            Description = dto.Description,
            VideogameId = dto.VideogameId,
            ScoreId = 0,
            Score = new Score(),
            VisibilityId = dto.VisibilityId,
            IncludesPorn = dto.IncludesPorn,
            IncludesGore = dto.IncludesGore,
            IncludesPolitics = dto.IncludesPolitics,
            IncludesSlurs = dto.IncludesSlurs,
            IncludesSensitiveContent = dto.IncludesSensitiveContent,
            CreatedAt = DateTime.UtcNow,
            ResourceVersions = [resourceVersion],
            ResourceTags = tags,
            ResourcesAuthors = authors!,
            ResourceExternalAuthors = externalAuthors,
            ResourceDependencies = dependencies!,
            ResourceExternalDependencies = externalDependencies,
            ResourcePreviews = resourcePreviews,
            ResourceResourceTypes = types!,
            RepositoryURL = dto.RepositoryURL,
        };

        _context.Resources.Add(res);

        return await _dbHelperService.TrySyncContextWithFiles(batchfile, res, ResourceServiceResult.DBError, ct);
    }

    private static string GetFullVersion(string? version)
    {
        return string.IsNullOrWhiteSpace(version) ?
                     "1.0.0" :
                     version.Trim();
    }

    private async Task<OneOf<Models.File, ResourceServiceResult>> GetMainFile(ResourceCreateDTO dto, string versionFolder, string mainFolder, string batchfile, CancellationToken ct)
    {
        string mainFilePath = GetVersionPath(dto.File.FileName, versionFolder, mainFolder);

        Models.File? file = await _fileService.PrepareFile(dto.File, mainFilePath, batchfile, ct);
        if (file == null)
            return ResourceServiceResult.FileCreationError;
        return file;
    }

    private static string GetVersionPath(string filename, string versionFolder, string mainFolder)
    {
        return Path.Combine(mainFolder, $"{Guid.NewGuid():N}_{versionFolder}",
                    Path.GetFileName(filename));
    }

    private ResourceServiceResult CheckResourceIsEditable(OneOf<Resource?, ResourceServiceResult> res)
    {
        if (res.TryPickT1(out var error, out var resource))
            return error;
        if (resource == null)
            return ResourceServiceResult.ResourceNotFound;
        string? userId = _accountService.GetUserID();
        if (userId == null)
            return ResourceServiceResult.UserNotFound;
        if(resource.SubmitterId == userId || 
            resource.ResourceVersions.Any(v => v.SubmitterUserId == userId) ||
            resource.ResourcesAuthors.Any(ra => ra.UserId == userId))
            return ResourceServiceResult.Success;
        return ResourceServiceResult.Unauthorized;
    }
    private ResourceServiceResult CheckResourceViability(OneOf<Resource?, ResourceServiceResult> res)
    {
        if (res.TryPickT1(out var error, out var resource))
            return error;
        if (resource == null)
            return ResourceServiceResult.ResourceNotFound;
        if (resource.VisibilityId != 3 || _accountService.IsModeratorOrAdmin())
            return ResourceServiceResult.Success;
        string? userId = _accountService.GetUserID();
        if (userId == null)
            return ResourceServiceResult.UserNotFound;
        if (userId == resource.SubmitterId)
            return ResourceServiceResult.Success;
        if (resource.ResourceVersions.Any(rv => rv.SubmitterUserId == userId))
            return ResourceServiceResult.Success;
        if (!resource.ResourcesAuthors.Any(ra => ra.UserId == userId))
            return ResourceServiceResult.Unauthorized;
        return ResourceServiceResult.Success;
    }
    private async Task<OneOf<List<ResourcePreview>, ResourceServiceResult>> GetPreviews(IFormFile[] formFiles, string mainFolder, string batchPath, CancellationToken ct)
    {
        List<Models.File> previews = [];

        foreach (var preview in formFiles)
        {
            var previewPath = Path.Combine(mainFolder, $"Previews",
                $"{Guid.NewGuid():N}{Path.GetFileName(preview.FileName)}");
            var previewFile = await _fileService.PrepareFile(preview, previewPath, batchPath, ct);
            if (previewFile == null)
                return ResourceServiceResult.FileCreationError;
            previews.Add(previewFile);
        }
        List<ResourcePreview> resourcePreviews = [.. previews
            .Select(pr => new ResourcePreview
            {
                Id = 0,
                FileId = 0,
                ResourceId = 0,
                File = pr
            })];
        return resourcePreviews;
    }

    private static ResourceVersion GetVersion(ResourceCreateDTO dto, string userId, string version, Models.File file)
    {
        return new()
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

    private static List<ResourceExternalDependency> GetExternalDependencies(ExternalDependencyDTO[] dtos)
    {
        return [.. dtos
            .Select(ed => new ResourceExternalDependency
            {
                Id = 0,
                ResourceId = 0,
                DependencyName = ed.DependencyName,
                DependencyURL = ed.DependencyURL,
                Mirror1 = ed.Mirror1,
                Mirror2 = ed.Mirror2,
                Mirror3 = ed.Mirror3,
                Mirror4 = ed.Mirror4
            })];
    }

    private async Task<List<ResourceResourceTypes?>> GetTypes(long[] typeIds, CancellationToken ct)
    {
        List<ResourceResourceTypes?> types = [];
        foreach (var typeid in typeIds)
        {
            var t = await _context.ResourceTypes.FirstOrDefaultAsync(r => r.Id == typeid, ct);

            types.Add(t != null ?
                new ResourceResourceTypes
                {
                    Id = 0,
                    ResourceId = 0,
                    ResourceTypeId = typeid
                } :
                null);
        }

        return types;
    }

    private async Task<List<ResourceDependency?>> GetDependencies(long[] dependencyIds, CancellationToken ct)
    {
        List<ResourceDependency?> dependencies = [];
        foreach (var dependencyId in dependencyIds)
        {
            var dependencyEntity = await _context.Resources.FirstOrDefaultAsync(r => r.Id == dependencyId, ct);

            dependencies.Add(dependencyEntity != null ?
                new ResourceDependency
                {
                    Id = 0,
                    ResourceId = 0,
                    DependencyResourceId = dependencyId
                } :
                null);
        }

        return dependencies;
    }

    private static List<ResourceExternalAuthor> GetExternalAuthors(string[] externalAuthors)
    {
        return [.. externalAuthors
            .Distinct()
            .Select(ea => new ResourceExternalAuthor
            {
                Author = ea,
                ResourceId = 0,
            })];
    }

    private async Task<List<ResourceAuthors?>> GetAuthors(string[] authorIDs, bool iAmAuthor, string userId, CancellationToken ct)
    {
        List<ResourceAuthors?> authors = [];

        foreach (var authorId in authorIDs)
        {
            var authorEntity = await _context.Users.FirstOrDefaultAsync(u => u.Id == authorId, ct);

            authors.Add(authorEntity != null ? 
                new ResourceAuthors
                {
                    UserId = authorId,
                    ResourceId = 0,
                    User = authorEntity,
                } :
                null);
        }

        if (iAmAuthor)
        {
            authors.Add(new ResourceAuthors
            {
                Id = 0,
                ResourceId = 0,
                UserId = userId
            });
        }

        return authors.Any(a => a == null) ?
            authors :
            [.. authors.DistinctBy(author => author!.UserId)];
    }

    private async Task<List<ResourceTags>> GetTags(string[] tagNames, CancellationToken ct)
    {
        var normalizedTags = tagNames
                    .Where(tag => !string.IsNullOrWhiteSpace(tag))
                    .Select(tag => tag.ToLower().Trim())
                    .Distinct();

        List<ResourceTags> tags = [];

        foreach (var tag in normalizedTags)
        {
            var t = await _context.Tags
                .FirstOrDefaultAsync(t => t.Name.ToLower().Trim() == tag, ct) ??
                    new Tag
                    {
                        Id = 0,
                        Name = char.ToUpperInvariant(tag[0]) + tag[1..]
                    };
            tags.Add(new ResourceTags
            {
                TagId = t.Id,
                Tag = t
            });
        }

        return tags;
    }
    private static string GetMainResourceFolder(Resource resource)
    {
        return string.Join('/', resource.ResourceVersions.First().File!.URL.Split('/')[..3]);
    }
    private static string SanitizeFolderName(string? name)
    {
        var sanitized = _sanitizeFolderRegex2().Replace(name ?? "", "_");

        sanitized = sanitized.Trim().TrimEnd(' ', '.');

        if (sanitized.Length == 0)
            return "folder";

        if (_sanitizeFolderRegex().IsMatch(sanitized))
            sanitized = "_" + sanitized;

        return sanitized;
    }
    [GeneratedRegex(@"^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\.|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _sanitizeFolderRegex();
    [GeneratedRegex(@"[<>:""/\\|?*\x00-\x1F]")]
    private static partial Regex _sanitizeFolderRegex2();
}
