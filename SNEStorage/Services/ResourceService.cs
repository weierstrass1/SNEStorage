using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SNEStorage.Services;

public enum ResourceServiceResult
{
    Success,
    InvalidSort,
    InvalidScoreRange,
    InvalidDateRange,
    InvalidName,
    InvalidDescription,
    InvalidRepositoryURL,
    InvalidReleaseNote,
    AmountOfElementsSurpassTheLimit,
    InvalidVersion,
    CacheError,
    InvalidMaxAmount,
    SkipOutOfRange,
    MaxAmountSurpassTheLimit,
    NegativePage,
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
    CacheService cacheService,
    FileService fileService,
    AccountService accountService,
    DBHelperService dbHelperService)
{
    public const int MIN_NAME_SIZE = 2;
    public const int MAX_NAME_SIZE = 100;
    public const int MIN_DESCRIPTION_SIZE = 10;
    public const int MAX_DESCRIPTION_SIZE = 10000;
    public const int MAX_AMOUNT_OF_ELEMENTS = 50;
    public const int MAX_VERSION_SIZE = 25;
    public const int MAX_AMOUNT_LIKE_LIMIT = 20;
    public const long FILE_MAX_SIZE = 250 * 1024 * 1024;
    public const long PREVIEW_MAX_SIZE = 5 * 1024 * 1024;
    public const int MAX_AMOUNT_OF_PREVIEWS = 20;
    public const int MAX_AMOUNT_PER_PAGE = 50;
    private readonly IWebHostEnvironment _environment = environment;
    private readonly SnestorageContext _context = context;
    private readonly CacheService _cacheService = cacheService;
    private readonly FileService _fileService = fileService;
    private readonly AccountService _accountService = accountService;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    public async Task<OneOf<List<ResourceFinderViewDTO>,ResourceServiceResult>> Find(ResourceFinderDTO finder, int maxAmount = 20, int page = 0, CancellationToken ct = default)
    {
        var validation = ValidateFinder(finder);
        if (validation != ResourceServiceResult.Success)
            return validation;
        if (maxAmount <= 0)
            return ResourceServiceResult.InvalidMaxAmount;
        if (maxAmount > MAX_AMOUNT_PER_PAGE)
            return ResourceServiceResult.MaxAmountSurpassTheLimit;
        if (page < 0)
            return ResourceServiceResult.NegativePage;
        long offset = (long)maxAmount * page;
        if (offset > int.MaxValue)
            return ResourceServiceResult.SkipOutOfRange;

        string? userId = _accountService.GetUserID();
        bool isModOrAdmin = _accountService.IsModeratorOrAdmin();
        var query = _context.Resources
            .AsNoTracking()
            .Where(r => r.VisibilityId == 1 || isModOrAdmin ||
                    (userId != null && (r.SubmitterId == userId ||
                    r.ResourcesAuthors.Any(a => a.UserId == userId) ||
                    r.ResourceVersions.Any(a => a.SubmitterUserId == userId))));
        query = FilterFind(query, finder);
        query = SortFind(query, finder.Sorting)
            .Skip((int)offset)
            .Take(maxAmount);

        return await SelectFind(query, ct);
    }
    public async Task<OneOf<List<Resource>,GetLikeResult>> GetResourceLike(string likeString, int maxAmount = 10, CancellationToken ct = default)
    {
        if (maxAmount <= 0)
            return GetLikeResult.InvalidMaxAmount;
        if (maxAmount > MAX_AMOUNT_LIKE_LIMIT)
            return GetLikeResult.MaxAmountSurpassTheLimit;

        bool isStaff = _accountService.IsModeratorOrAdmin();
        string? userId = _accountService.GetUserID();
        var term = likeString?.ToLower().Trim() ?? "";
        var query = _context.Resources
            .AsNoTracking()
            .Where(r => r.VisibilityId != 3 || isStaff ||
                (userId != null && 
                (r.SubmitterId == userId || 
                r.ResourcesAuthors.Any(a => a.UserId == userId) ||
                r.ResourceVersions.Any(a => a.SubmitterUserId == userId))));

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

        return await _dbHelperService.TryFunction(
            async cancel => await query
            .OrderBy(v => v.Name)
            .Take(maxAmount)
            .ToListAsync(cancel), GetLikeResult.DBError, ct: ct);
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
    public async Task<OneOf<ScoreUsers, ResourceServiceResult>> AddOrEditScore(
        long resourceId, long value, CancellationToken ct = default)
    {
        if (value is < 1 or > 5)
            return ResourceServiceResult.InvalidScore;

        var userId = _accountService.GetUserID();
        if (userId == null)
            return ResourceServiceResult.Unauthorized;

        var accessRes = await GetAccess(resourceId, ct);
        if (!accessRes.TryPickT0(out var access, out var error))
            return error;

        if (access == null)
            return ResourceServiceResult.ResourceNotFound;
        if (access.VisibilityId == 3)
            return ResourceServiceResult.CantAddScoreToPrivateResource;
        if (access.IsContributor)
            return ResourceServiceResult.CantScoreSelfResource;

        var voteRes = await _dbHelperService.TryFunction(
            cancel => _context.ScoresUsers.AsTracking()
                .FirstOrDefaultAsync(
                    s => s.ScoreId == access.ScoreId && s.UserId == userId,
                    cancel),
            ResourceServiceResult.DBError, ct: ct);

        if (!voteRes.TryPickT0(out var vote, out error))
            return error;

        if (vote == null)
        {
            vote = new ScoreUsers { ScoreId = access.ScoreId, UserId = userId };
            _context.ScoresUsers.Add(vote);
        }

        vote.Value = value;
        return await _dbHelperService.TrySyncContext(
            vote, ResourceServiceResult.DBError, ct);
    }
    public async Task<OneOf<ResourceServiceResult, FileServiceResult>> Edit(long resourceId, ResourceEditDTO dto, CancellationToken ct = default)
    {
        if (dto.Name != null)
        {
            dto.Name = dto.Name.Trim();
            if (string.IsNullOrWhiteSpace(dto.Name))
                return ResourceServiceResult.EmptyName;
            if (dto.Name.Length < MIN_NAME_SIZE || dto.Name.Length > MAX_NAME_SIZE ||
                !ResourceNameRegex().IsMatch(dto.Name))
                return ResourceServiceResult.InvalidName;
        }
        if (dto.Description != null)
        {
            dto.Description = dto.Description.Trim();
            if (string.IsNullOrWhiteSpace(dto.Description))
                return ResourceServiceResult.EmptyDescription;
            if (dto.Description.Length < MIN_DESCRIPTION_SIZE ||
                dto.Description.Length > MAX_DESCRIPTION_SIZE)
                return ResourceServiceResult.InvalidDescription;
        }
        if(dto.RepositoryURL != null)
        {
            dto.RepositoryURL = dto.RepositoryURL.Trim();
            if (dto.RepositoryURL.Length > 0 &&
                !IsValidRepositoryUrl(dto.RepositoryURL))
                return ResourceServiceResult.InvalidRepositoryURL;
        }
        if (dto.Previews.Any(pr => pr.Length > PREVIEW_MAX_SIZE))
            return ResourceServiceResult.PreviewSizeSurpassTheLimit;
        var access = CheckEditAccess(await GetAccess(resourceId, ct));
        if (access != ResourceServiceResult.Success)
            return access;

        var res = await _dbHelperService.TryFunction(
            cancel => _context.Resources
                .AsTracking()
                .Where(resource => resource.Id == resourceId)
                .Include(resource => resource.ResourceVersions)
                    .ThenInclude(version => version.File)
                .Include(resource => resource.ResourcePreviews)
                .Include(resource => resource.ResourcesAuthors)
                .Include(resource => resource.ResourceExternalAuthors)
                .Include(resource => resource.ResourceDependencies)
                .Include(resource => resource.ResourceExternalDependencies)
                .Include(resource => resource.ResourceResourceTypes)
                .Include(resource => resource.ResourceTags)
                .AsSplitQuery()
                .FirstOrDefaultAsync(cancel),
            ResourceServiceResult.DBError, ct: ct);

        if (!res.TryPickT0(out var resource, out var error))
            return error;
        if (resource == null)
            return ResourceServiceResult.ResourceNotFound;

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

        if(tags == null)
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ResourceServiceResult.DBError;
        }

        var typesRes = await _dbHelperService.TryFunction(
            async cancel => await GetTypes(dto.Types, cancel),
            ResourceServiceResult.DBError,
            async () => await _fileService.BatchRollback(batchfile, ct), ct);
        if (!typesRes.TryPickT0(out var types, out var typeError))
            return typeError;

        if (types == null)
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ResourceServiceResult.CacheError;
        }

        if (types.Any(t => t == null))
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ResourceServiceResult.ResourceTypeNotFound;
        }

        var authorRes = await _dbHelperService.TryFunction(
            async cancel => await GetAuthors(dto.AuthorsIDs, dto.IAmAuthor == true , userId, ct),
            ResourceServiceResult.DBError,
            async () => await _fileService.BatchRollback(batchfile, ct), ct);
        if (!authorRes.TryPickT0(out var authors, out var authorError))
            return authorError;

        if (authors == null || authors.Any(t => t == null))
        {
            await _fileService.BatchRollback(batchfile, ct);
            return authors != null ?
                ResourceServiceResult.UserNotFound :
                ResourceServiceResult.DBError;
        }

        List<ResourceExternalAuthor> externalAuthors = GetExternalAuthors(dto.ExternalAuthors);

        var dependenciesRes = await _dbHelperService.TryFunction(
            async cancel => await GetDependencies(dto.DependencyIDs, ct),
            ResourceServiceResult.DBError,
            async () => await _fileService.BatchRollback(batchfile, ct), ct);
        if (!dependenciesRes.TryPickT0(out var dependencies, out var dependenciesError))
            return dependenciesError;

        if (dependencies == null || dependencies.Any(t => t == null))
        {
            await _fileService.BatchRollback(batchfile, ct);
            return dependencies != null ?
                ResourceServiceResult.ResourceNotFound :
                ResourceServiceResult.DBError;
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
            .Where(d => d!.DependencyResourceId != resourceId)
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

        if (resource.ResourcePreviews.Count > MAX_AMOUNT_OF_PREVIEWS)
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ResourceServiceResult.PreviewAmountSurpassTheLimit;
        }

        if (resource.ResourcesAuthors.Count + resource.ResourceExternalAuthors.Count > MAX_AMOUNT_OF_ELEMENTS ||
            resource.ResourceDependencies.Count + resource.ResourceExternalDependencies.Count > MAX_AMOUNT_OF_ELEMENTS ||
            resource.ResourceTags.Count > MAX_AMOUNT_OF_ELEMENTS)
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ResourceServiceResult.AmountOfElementsSurpassTheLimit;
        }

        var removePrs = await _dbHelperService.TryFunction(
                            async cancel => await _context.ResourcePreviews
                                .AsNoTracking()
                                .Where(pr => pr.ResourceId == resourceId && prIds.Contains(pr.Id))
                                .ToListAsync(cancel),
                            async () => await _fileService.BatchRollback(batchfile, ct), ct);
        if(removePrs == null)
            return ResourceServiceResult.DBError;

        var files = await _dbHelperService.TryFunction(
            async cancel => await _fileService
                .PrepareDeletion(removePrs.Select(pr => pr.FileId), cancel),
            async () => await _fileService.BatchRollback(batchfile, ct), ct);
        if(files == null)
            return ResourceServiceResult.DBError;

        if (!await _fileService.TrySyncContextWithFiles(batchfile, ct)) 
            return ResourceServiceResult.DBError;

        _fileService.FinishDeletion(files);
        return ResourceServiceResult.Success;
    }
    public async Task<OneOf<ResourceVersion, ResourceServiceResult, FileServiceResult>> AddVersion(long resourceId, ResourceVersionCreateDTO dto, CancellationToken ct = default)
    {
        if (dto.File.Length > FILE_MAX_SIZE)
            return ResourceServiceResult.FileSizeSurpassTheLimit;
        string? userId = _accountService.GetUserID();
        if (userId == null)
            return ResourceServiceResult.Unauthorized;

        dto.Version = dto.Version.ToLower().Trim();

        if (!await ValidateVersion(resourceId, null, dto.Version, ct))
            return ResourceServiceResult.InvalidVersion;

        dto.ReleaseNotes = dto.ReleaseNotes.Trim();

        if (dto.ReleaseNotes.Length < MIN_DESCRIPTION_SIZE || dto.ReleaseNotes.Length > MAX_DESCRIPTION_SIZE)
            return ResourceServiceResult.InvalidReleaseNote;

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

        var fileRes = await _fileService.PrepareFile(FileContext.Resource, dto.File, path, batchPath, ct);
        if(fileRes.TryPickT1(out var ferror, out var file))
        {
            await _fileService.BatchRollback(batchPath, ct);
            return ferror;
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

        if (dto.Version != null)
        {
            dto.Version = dto.Version.ToLower().Trim();

            if (!await ValidateVersion(resourceId, versionId, dto.Version, ct))
                return ResourceServiceResult.InvalidVersion;
        }
        if(dto.ReleaseNotes != null)
        {
            dto.ReleaseNotes = dto.ReleaseNotes.Trim();
            if (dto.ReleaseNotes.Length < MIN_DESCRIPTION_SIZE || dto.ReleaseNotes.Length > MAX_DESCRIPTION_SIZE)
                return ResourceServiceResult.InvalidReleaseNote;
        }

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
    public async Task<OneOf<ResourceReadDTO, ResourceServiceResult>> Get(
        long resourceId,
        CancellationToken ct = default)
    {
        var accessResult = await GetAccess(resourceId, ct);
        var access = CheckViewAccess(accessResult);
        if (access != ResourceServiceResult.Success)
            return access;

        bool canEdit = CheckEditAccess(accessResult)
            == ResourceServiceResult.Success;

        var result = await _dbHelperService.TryFunction(
            cancel => _context.Resources
                .AsNoTracking()
                .Where(resource => resource.Id == resourceId)
                .Select(resource => new ResourceReadDTO
                {
                    Id = resource.Id,
                    Name = resource.Name,
                    Description = resource.Description,
                    CreatedAt = resource.CreatedAt,
                    UpdatedAt = resource.UpdatedAt,
                    RepositoryURL = resource.RepositoryURL,
                    AlreadyModerated = resource.AlreadyModerated,
                    CanEdit = canEdit,

                    IncludesPorn = resource.IncludesPorn,
                    IncludesGore = resource.IncludesGore,
                    IncludesPolitics = resource.IncludesPolitics,
                    IncludesSlurs = resource.IncludesSlurs,
                    IncludesSensitiveContent = resource.IncludesSensitiveContent,

                    Videogame = new IdNamePair
                    {
                        Id = resource.VideogameId,
                        Name = resource.Videogame!.Name ?? "Unknown"
                    },
                    Visibility = new IdNamePair
                    {
                        Id = resource.VisibilityId,
                        Name = resource.Visibility!.Name ?? "Unknown"
                    },
                    Types = resource.ResourceResourceTypes
                        .OrderBy(type => type.ResourceType!.Name)
                        .ThenBy(type => type.Id)
                        .Select(type => new ResourceTypeReadDTO(
                            type.Id,
                            type.ResourceTypeId,
                            type.ResourceType!.Name ?? "Unknown",
                            type.ResourceType.CategoryId,
                            type.ResourceType.Category!.Name))
                        .ToList(),
                    Tags = resource.ResourceTags
                        .OrderBy(tag => tag.Tag!.Name)
                        .ThenBy(tag => tag.Id)
                        .Select(tag => new ResourceTagReadDTO(
                            tag.Id,
                            tag.TagId,
                            tag.Tag!.Name))
                        .ToList()
                })
                .AsSplitQuery()
                .FirstOrDefaultAsync(cancel),
            ResourceServiceResult.DBError,
            ct: ct);

        return ItemOrError(result);
    }
    public async Task<OneOf<Models.File, ResourceServiceResult>> GetFile(long resourceId,
        long resourceFileId, bool prepareDownload = false, CancellationToken ct = default)
    {
        var access = CheckViewAccess(await GetAccess(resourceId, ct));
        if (access != ResourceServiceResult.Success)
            return access;

        IQueryable<Models.File> query = _context.Files
            .AsNoTracking()
            .Where(file =>
                file.Id == resourceFileId &&
                (_context.ResourceVersions.Any(version =>
                    version.ResourceId == resourceId && version.FileId == file.Id) ||
                _context.ResourcePreviews.Any(preview =>
                        preview.ResourceId == resourceId && preview.FileId == file.Id)));

        if (prepareDownload)
            query = query.Include(file => file.FileType);

        var result = await _dbHelperService.TryFunction(
            cancel => query.FirstOrDefaultAsync(cancel),
            ResourceServiceResult.DBError,
            ct: ct);

        return ItemOrError(result, ResourceServiceResult.FileNotFound);
    }
    public async Task<OneOf<(string, string), ResourceServiceResult>> Download(long resourceId,
        long resourceFileId, CancellationToken ct = default)
    {
        var result = await GetFile(resourceId, resourceFileId, true, ct);

        if (!result.TryPickT0(out var file, out var error))
            return error;

        var physicalPath = Path.Combine(
            _environment.ContentRootPath,
            file.URL);

        if (!System.IO.File.Exists(physicalPath))
            return ResourceServiceResult.FileNotFound;

        var updateResult = await _dbHelperService.TryFunction(
                async cancel => await _context.ResourceVersions
                    .Where(version => version.ResourceId == resourceId &&
                        version.FileId == resourceFileId)
                    .ExecuteUpdateAsync(setters => setters
                        .SetProperty(version => version.Downloads,
                            version => version.Downloads + 1),
                        cancel),
                ResourceServiceResult.DBError, ct: ct);

        if (updateResult.TryPickT1(out var updateError, out _))
            return updateError;

        var mime = file.FileType?.Value ?? "application/octet-stream";

        return (physicalPath, mime);
    }
    public async Task<OneOf<Resource, ResourceServiceResult, FileServiceResult>> Create(ResourceCreateDTO dto, CancellationToken ct = default)
    {
        dto.Name = dto.Name.Trim();
        dto.Description = dto.Description.Trim();
        dto.RepositoryURL = dto.RepositoryURL?.Trim();
        dto.ChangeLog = dto.ChangeLog.Trim();

        if (string.IsNullOrWhiteSpace(dto.Name))
            return ResourceServiceResult.EmptyName;
        if (dto.Name.Length < MIN_NAME_SIZE || dto.Name.Length > MAX_NAME_SIZE || 
            !ResourceNameRegex().IsMatch(dto.Name))
            return ResourceServiceResult.InvalidName;
        if (string.IsNullOrWhiteSpace(dto.Description))
            return ResourceServiceResult.EmptyDescription;
        if (dto.Description.Length < MIN_DESCRIPTION_SIZE || dto.Description.Length > MAX_DESCRIPTION_SIZE)
            return ResourceServiceResult.InvalidDescription;
        if (dto.ChangeLog.Length < MIN_DESCRIPTION_SIZE || dto.ChangeLog.Length > MAX_DESCRIPTION_SIZE)
            return ResourceServiceResult.InvalidReleaseNote;
        if (dto.RepositoryURL != null && !IsValidRepositoryUrl(dto.RepositoryURL))
            return ResourceServiceResult.InvalidRepositoryURL;
        if (dto.File.Length > FILE_MAX_SIZE)
            return ResourceServiceResult.FileSizeSurpassTheLimit;
        if (dto.Previews.Any(pr => pr.Length > PREVIEW_MAX_SIZE))
            return ResourceServiceResult.PreviewSizeSurpassTheLimit;
        if (dto.Previews.Length > MAX_AMOUNT_OF_PREVIEWS)
            return ResourceServiceResult.PreviewAmountSurpassTheLimit;
        if (dto.Types is null || dto.Types.Length == 0)
            return ResourceServiceResult.EmptyTypeList;
        if (dto.AuthorsIDs.Length + dto.ExternalAuthors.Length > MAX_AMOUNT_OF_ELEMENTS ||
            dto.DependencyIDs.Length + dto.ExternalDependencies.Length > MAX_AMOUNT_OF_ELEMENTS ||
            dto.Tags.Length > MAX_AMOUNT_OF_ELEMENTS)
            return ResourceServiceResult.AmountOfElementsSurpassTheLimit;

        var userId = _accountService.GetUserID();
        if (string.IsNullOrWhiteSpace(userId))
            return ResourceServiceResult.UserNotFound;

        dto.Version = dto.Version.ToLower().Trim();

        if (!await ValidateVersion(null, null, dto.Version, ct)) 
            return ResourceServiceResult.InvalidVersion;

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
        if (mainFileRes.IsT2)
            return mainFileRes.AsT2;

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

        if (tags == null)
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ResourceServiceResult.DBError;
        }

        var typesRes = await _dbHelperService.TryFunction(
            async cancel => await GetTypes(dto.Types, cancel),
            ResourceServiceResult.DBError,
            async () => await _fileService.BatchRollback(batchfile, ct), ct);
        if (!typesRes.TryPickT0(out var types, out var typeError))
            return typeError;

        if (types == null)
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ResourceServiceResult.CacheError;
        }

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

        if (authors == null || authors.Any(t => t == null))
        {
            await _fileService.BatchRollback(batchfile, ct);
            return authors != null ?
                ResourceServiceResult.UserNotFound :
                ResourceServiceResult.DBError;
        }

        List<ResourceExternalAuthor> externalAuthors = GetExternalAuthors(dto.ExternalAuthors);

        var dependenciesRes = await _dbHelperService.TryFunction(
            async cancel => await GetDependencies(dto.DependencyIDs, ct),
            ResourceServiceResult.DBError,
            async () => await _fileService.BatchRollback(batchfile, ct), ct);
        if (!dependenciesRes.TryPickT0(out var dependencies, out var dependenciesError))
            return dependenciesError;

        if (dependencies == null || dependencies.Any(t => t == null))
        {
            await _fileService.BatchRollback(batchfile, ct);
            return dependencies != null ?
                ResourceServiceResult.ResourceNotFound :
                ResourceServiceResult.DBError;
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

        var result = await _fileService.TrySyncContextWithFiles(batchfile, res, ResourceServiceResult.DBError, ct);
        return result.IsT0 ?
            result.AsT0 : result.AsT1;
    }

    private static string GetFullVersion(string? version)
    {
        return string.IsNullOrWhiteSpace(version) ?
                     "1.0.0" :
                     version.Trim();
    }

    private async Task<OneOf<Models.File, ResourceServiceResult, FileServiceResult>> GetMainFile(ResourceCreateDTO dto, string versionFolder, string mainFolder, string batchfile, CancellationToken ct)
    {
        string mainFilePath = GetVersionPath(dto.File.FileName, versionFolder, mainFolder);

        var fileRes = await _fileService.PrepareFile(FileContext.Resource, dto.File, mainFilePath, batchfile, ct);
        if (fileRes.TryPickT1(out var error, out var file))
            return error;
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
    private async Task<OneOf<List<ResourcePreview>, FileServiceResult>> GetPreviews(IFormFile[] formFiles, string mainFolder, string batchPath, CancellationToken ct)
    {
        List<Models.File> previews = [];

        foreach (var preview in formFiles)
        {
            var previewPath = Path.Combine(mainFolder, $"Previews",
                $"{Guid.NewGuid():N}{Path.GetFileName(preview.FileName)}");
            var previewFileRes = await _fileService.PrepareFile(FileContext.Previews, preview, previewPath, batchPath, ct);
            if (previewFileRes.TryPickT1(out var error, out var previewFile))
                return error;
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

    private async Task<List<ResourceResourceTypes?>?> GetTypes(long[] typeIds, CancellationToken ct)
    {
        List<ResourceResourceTypes?> types = [];

        var tRes = await _cacheService.GetAllTypes(ct);
        if (tRes == null)
            return null;

        foreach (var typeid in typeIds)
        {
            tRes.TryGetValue(typeid, out var t);

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

    private async Task<List<ResourceDependency?>?> GetDependencies(long[] dependencyIds, 
        CancellationToken ct)
    {
        var ids = dependencyIds.Distinct().ToArray();
        if (ids.Length == 0)
            return [];

        var existingIds = await _dbHelperService.TryFunction(
            async cancel => await _context.Resources
                .AsNoTracking()
                .Where(resource => ids.Contains(resource.Id))
                .Select(resource => resource.Id)
                .ToListAsync(cancel), ct: ct);
        if (existingIds == null)
            return null;

        var existing = existingIds.ToHashSet();
        List<ResourceDependency?> dependencies = [];

        foreach (var id in ids)
        {
            dependencies.Add(existing.Contains(id)
                ? new ResourceDependency
                {
                    Id = 0,
                    ResourceId = 0,
                    DependencyResourceId = id
                }
                : null);
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
    private async Task<List<ResourceAuthors?>?> GetAuthors(string[] authorIDs, 
        bool iAmAuthor, string userId, CancellationToken ct)
    {
        var ids = authorIDs
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (iAmAuthor && !ids.Contains(userId, StringComparer.OrdinalIgnoreCase))
            ids.Add(userId);

        if (ids.Count == 0)
            return [];

        var users = await _dbHelperService.TryFunction(
            async cancel => await _context.Users
            .AsTracking()
            .Where(user => ids.Contains(user.Id))
            .ToListAsync(cancel), ct: ct);

        if (users == null)
            return null;

        var usersById = users.ToDictionary(user => user.Id,
            StringComparer.OrdinalIgnoreCase);
        List<ResourceAuthors?> authors = [];

        foreach (var id in ids)
        {
            authors.Add(usersById.TryGetValue(id, out var user)
                ? new ResourceAuthors
                {
                    ResourceId = 0,
                    UserId = user.Id,
                    User = user
                }
                : null);
        }

        return authors;
    }
    private async Task<List<ResourceTags>?> GetTags(string[] tagNames, CancellationToken ct)
    {
        var names = tagNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.ToLower().Trim())
            .Distinct()
            .ToArray();

        if (names.Length == 0)
            return [];

        var existingTags = await _dbHelperService.TryFunction(
            async cancel => await _context.Tags
                .AsTracking()
                .Where(tag => names.Contains(tag.Name.ToLower().Trim()))
                .OrderBy(tag => tag.Id)
                .ToListAsync(cancel), ct: ct);

        if (existingTags == null)
            return null;

        var tagsByName = existingTags
            .GroupBy(tag => tag.Name.ToLower().Trim())
            .ToDictionary(group => group.Key, group => group.First());

        List<ResourceTags> tags = [];

        foreach (var name in names)
        {
            if (!tagsByName.TryGetValue(name, out var tag))
            {
                tag = new Tag
                {
                    Id = 0,
                    Name = char.ToUpperInvariant(name[0]) + name[1..]
                };
            }

            tags.Add(new ResourceTags
            {
                TagId = tag.Id,
                Tag = tag
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
    public async Task<OneOf<List<ResourceDependencyViewDTO>, ResourceServiceResult>> GetResourceAllDependencies(long resourceId, CancellationToken ct)
    {
        var access = CheckViewAccess(await GetAccess(resourceId, ct));
        if (access != ResourceServiceResult.Success)
            return access;

        var res = await _dbHelperService.TryFunction(
            async cancel => await _context.ResourceDependencies
            .AsNoTracking()
            .Where(r => r.ResourceId == resourceId)
            .OrderByDescending(v => v.DependencyResource!.Name)
            .ThenBy(v => v.Id)
            .Select(r => new ResourceDependencyViewDTO(r.Id, r.
                DependencyResourceId, 
                r.DependencyResource!.Name))
            .ToListAsync(cancel),
            ResourceServiceResult.DBError, ct: ct);
        return ItemOrError(res!)!;
    }
    public async Task<OneOf<List<ResourceExternalDependency>, ResourceServiceResult>> GetResourceAllExternalDependencies(long resourceId, CancellationToken ct)
    {
        var access = CheckViewAccess(await GetAccess(resourceId, ct));
        if (access != ResourceServiceResult.Success)
            return access;

        var res = await _dbHelperService.TryFunction(
            async cancel => await _context.ResourceExternalDependencies
            .AsNoTracking()
            .Where(r => r.ResourceId == resourceId)
            .OrderByDescending(v => v.DependencyName)
            .ThenBy(v => v.Id)
            .ToListAsync(cancel),
            ResourceServiceResult.DBError, ct: ct);
        return ItemOrError(res!)!;
    }
    public async Task<OneOf<List<ResourceExternalAuthor>, ResourceServiceResult>> GetResourceAllExternalAuthors(long resourceId, CancellationToken ct)
    {
        var access = CheckViewAccess(await GetAccess(resourceId, ct));
        if (access != ResourceServiceResult.Success)
            return access;

        var res = await _dbHelperService.TryFunction(
            async cancel => await _context.ResourcesExternalAuthors
            .AsNoTracking()
            .Where(r => r.ResourceId == resourceId)
            .OrderByDescending(v => v.Author)
            .ThenBy(v => v.Id)
            .ToListAsync(cancel),
            ResourceServiceResult.DBError, ct: ct);
        return ItemOrError(res!)!;
    }
    public async Task<OneOf<List<ResourceAuthorDTO>, ResourceServiceResult>> GetResourceAllAuthors(long resourceId, CancellationToken ct)
    {
        var access = CheckViewAccess(await GetAccess(resourceId, ct));
        if (access != ResourceServiceResult.Success)
            return access;

        var res = await _dbHelperService.TryFunction(
            async cancel => await _context.ResourcesAuthors
            .AsNoTracking()
            .Where(r => r.ResourceId == resourceId)
            .Select(r => new ResourceAuthorDTO
            {
                Id = r.Id,
                UserSummary = new UserNameDTO
                {
                    Id = r.UserId,
                    Username = r.User!.UserName!,
                    UsernameColor = r.User.UserInfo!.UsernameColor,
                    Avatar = new FileResponse
                    {
                        Id = r.User.UserInfo.Avatar!.Id,
                        FileTypeId = r.User.UserInfo.Avatar.FileTypeId,
                        URL = r.User.UserInfo.Avatar.URL,
                    }
                }
            })
            .OrderByDescending(v => v.UserSummary.Username)
            .ThenBy(v => v.Id)
            .ToListAsync(cancel),
            ResourceServiceResult.DBError, ct: ct);
        return ItemOrError(res!)!;
    }
    public async Task<OneOf<List<ResourceVersion>, ResourceServiceResult>> GetResourceAllVersion(long resourceId, CancellationToken ct)
    {
        var access = CheckViewAccess(await GetAccess(resourceId, ct));
        if (access != ResourceServiceResult.Success)
            return access;

        var res = await _dbHelperService.TryFunction(
            async cancel => await _context.ResourceVersions
            .AsNoTracking()
            .Where(r => r.ResourceId == resourceId)
            .Include(r => r.File)
            .OrderByDescending(v => v.CreatedAt)
            .ThenByDescending(v => v.Id)
            .ToListAsync(cancel),
            ResourceServiceResult.DBError, ct: ct);
        return ItemOrError(res!)!;
    }
    public async Task<OneOf<List<ResourcePreview>, ResourceServiceResult>> GetResourceAllPreviews(long resourceId, CancellationToken ct)
    {
        var access = CheckViewAccess(await GetAccess(resourceId, ct));
        if (access != ResourceServiceResult.Success)
            return access;

        var res = await _dbHelperService.TryFunction(
            async cancel => await _context.ResourcePreviews
            .AsNoTracking()
            .Where(r => r.ResourceId == resourceId)
            .Include(r => r.File)
            .OrderBy(v => v.Id)
            .ToListAsync(cancel),
            ResourceServiceResult.DBError, ct: ct);
        return ItemOrError(res!)!;
    }
    public async Task<OneOf<ResourceVersion, ResourceServiceResult>> GetResourceLastVersion(long resourceId, CancellationToken ct)
    {
        var access = CheckViewAccess(await GetAccess(resourceId, ct));
        if (access != ResourceServiceResult.Success)
            return access;

        var res = await _dbHelperService.TryFunction(
            async cancel => await _context.ResourceVersions
            .AsNoTracking()
            .Include(r => r.File)
            .Where(r => r.ResourceId == resourceId)
            .OrderByDescending(v => v.CreatedAt)
            .ThenByDescending(v => v.Id)
            .Take(1)
            .FirstOrDefaultAsync(cancel),
            ResourceServiceResult.DBError, ct: ct);
        return ItemOrError(res, ResourceServiceResult.VersionNotFound)!;
    }
    public async Task<OneOf<RatingReadDto,ResourceServiceResult>> GetRating(long resourceId, CancellationToken ct)
    {
        var access = CheckViewAccess(await GetAccess(resourceId, ct));
        if (access != ResourceServiceResult.Success)
            return access;

        var userId = _accountService.GetUserID();
        var res = await _dbHelperService.TryFunction(
            async cancel => await _context.Resources.AsNoTracking()
            .Where(r => r.Id == resourceId)
            .Select(r => new RatingReadDto(
                r.Score!.ScoresUsers.Average(v => (double?)v.Value) ?? 0.0,
                r.Score.ScoresUsers.Count(),
                r.Score.ScoresUsers
                    .Where(v => userId != null && v.UserId == userId)
                    .Select(v => (long?)v.Value)
                    .FirstOrDefault()))
            .FirstOrDefaultAsync(cancel),
            ResourceServiceResult.DBError, ct: ct);
        if (!res.TryPickT0(out var dto, out var error))
            return error;
        return dto != null ?
            dto :
            ResourceServiceResult.ResourceNotFound;
    }
    private async Task<OneOf<ResourceAccessRow?, ResourceServiceResult>> GetAccess(long resourceId, CancellationToken ct)
    {
        var userId = _accountService.GetUserID();
        var res = await _dbHelperService.TryFunction(
            async cancel => await _context.Resources
                .AsNoTracking()
                .Where(r => r.Id == resourceId)
                .Select(r => new ResourceAccessRow(
                    r.Id, r.ScoreId, r.VisibilityId,
                    userId != null && (
                        r.SubmitterId == userId ||
                        r.ResourcesAuthors.Any(a => a.UserId == userId) ||
                        r.ResourceVersions.Any(v => v.SubmitterUserId == userId))))
                .FirstOrDefaultAsync(cancel),
            ResourceServiceResult.DBError, ct: ct);
        return ItemOrError(res)!;
    }
    private ResourceServiceResult CheckViewAccess(OneOf<ResourceAccessRow?, ResourceServiceResult> accessRes)
    {
        if (accessRes.TryPickT1(out var error, out var access))
            return error;
        if (access is null)
            return ResourceServiceResult.ResourceNotFound;
        if (access.VisibilityId != 3 || _accountService.IsModeratorOrAdmin())
            return ResourceServiceResult.Success;
        if (_accountService.GetUserID() is null)
            return ResourceServiceResult.UserNotFound;
        return access.IsContributor
            ? ResourceServiceResult.Success : ResourceServiceResult.Unauthorized;
    }

    private ResourceServiceResult CheckEditAccess(OneOf<ResourceAccessRow?, ResourceServiceResult> accessRes)
    {
        if (accessRes.TryPickT1(out var error, out var access))
            return error;
        if (access is null)
            return ResourceServiceResult.ResourceNotFound;
        if (_accountService.GetUserID() is null)
            return ResourceServiceResult.UserNotFound;
        return access.IsContributor
            ? ResourceServiceResult.Success : ResourceServiceResult.Unauthorized;
    }
    private static OneOf<T, ResourceServiceResult> ItemOrError<T>(OneOf<T?, ResourceServiceResult> item, ResourceServiceResult notFoundError = ResourceServiceResult.ResourceNotFound)
    {
        if (!item.TryPickT0(out var i, out var error))
            return error;
        return i != null ?
            i :
            notFoundError;
    }
    private static IQueryable<Resource> SortFind(IQueryable<Resource> query, ResourceSortingDTO sort)
    {
        var ordered = sort.SortType switch
        {
            ResourceSorting.BestScore => sort.SortOrderDescending ?
                query.OrderByDescending(r => r.Score!.ScoresUsers
                    .Average(su => (double?)su.Value) ?? 0.0) :
                query.OrderBy(r => r.Score!.ScoresUsers
                    .Average(su => (double?)su.Value) ?? 0.0),
            ResourceSorting.Downloads => sort.SortOrderDescending ?
                query.OrderByDescending(r => r.ResourceVersions.Sum(rv => (long?)rv.Downloads) ?? 0L) :
                query.OrderBy(r => r.ResourceVersions.Sum(rv => (long?)rv.Downloads) ?? 0L),
            ResourceSorting.Name => sort.SortOrderDescending ?
                query.OrderByDescending(r => r.Name) :
                query.OrderBy(r => r.Name),
            ResourceSorting.NumberOfVotes => sort.SortOrderDescending ?
                query.OrderByDescending(r => r.Score!.ScoresUsers.Count) :
                query.OrderBy(r => r.Score!.ScoresUsers.Count),
            ResourceSorting.VersionCreatedAt => sort.SortOrderDescending ?
                query.OrderByDescending(r => r.ResourceVersions
                    .OrderByDescending(rv => rv.CreatedAt)
                    .ThenByDescending(rv => rv.Id)
                    .Select(rv => rv.CreatedAt)
                    .First()!) :
                query.OrderBy(r => r.ResourceVersions
                    .OrderByDescending(rv => rv.CreatedAt)
                    .ThenByDescending(rv => rv.Id)
                    .Select(rv => rv.CreatedAt)
                    .First()!),
            _ => sort.SortOrderDescending ?
                query.OrderByDescending(r => r.CreatedAt) :
                query.OrderBy(r => r.CreatedAt),
        };
        return ordered.ThenByDescending(r => r.Id);
    }
    private static IQueryable<Resource> FilterFind(IQueryable<Resource> query, ResourceFinderDTO dto)
    {
        if (dto.AuthorsIDs.Length > 0)
            query = query.Where(r =>
                r.ResourcesAuthors.Any(a => dto.AuthorsIDs.Contains(a.UserId)));
        if (!dto.CanIncludesGore)
            query = query
                .Where(r => !r.IncludesGore);
        if (!dto.CanIncludesPolitics)
            query = query
                .Where(r => !r.IncludesPolitics);
        if (!dto.CanIncludesPorn)
            query = query
                .Where(r => !r.IncludesPorn);
        if (!dto.CanIncludesSensitiveContent)
            query = query
                .Where(r => !r.IncludesSensitiveContent);
        if (!dto.CanIncludesSlurs)
            query = query
                .Where(r => !r.IncludesSlurs);
        if (dto.CategoryIDs.Length > 0)
            query = query.Where(r =>
                r.ResourceResourceTypes.Any(t =>
                    dto.CategoryIDs.Contains(t.ResourceType!.CategoryId)));
        if (!string.IsNullOrWhiteSpace(dto.AuthorLike))
        {
            var escaped = dto.AuthorLike.Trim()
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_")
                .Replace("[", @"\[");

            var pattern = $"%{escaped}%";

            query = query.Where(resource =>
                resource.ResourcesAuthors.Any(author =>
                    EF.Functions.Like(
                        author.User!.UserName!,
                        pattern,
                        @"\")) ||
                resource.ResourceExternalAuthors.Any(author =>
                    EF.Functions.Like(
                        author.Author,
                        pattern,
                        @"\")));
        }
        if (dto.CreatedAtFrom is { } from)
        {
            var limit = from.Date;
            query = query.Where(r => r.CreatedAt >= limit);
        }
        if (dto.CreatedAtTo is { } to)
        {
            var limit = to.Date.AddDays(1);
            query = query.Where(r => r.CreatedAt < limit);
        }
        if (dto.MustHaveScore != null)
            query = query
                .Where(r => r.Score!.ScoresUsers.Any() == dto.MustHaveScore);
        if (dto.NameLike != null)
        {
            var escaped = dto.NameLike
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_")
                .Replace("[", @"\[");

            var pattern = $"%{escaped}%";
            query = query
                .Where(r => EF.Functions.Like(r.Name, pattern, @"\"));
        }
        if (dto.ScoreHigherLimit != null)
            query = query
                .Where(r => (r.Score!.ScoresUsers.Average(ru => (double?)ru.Value) ?? 0.0) <=
                    dto.ScoreHigherLimit);
        if (dto.ScoreLowerLimit != null)
            query = query
                .Where(r => (r.Score!.ScoresUsers.Average(ru => (double?)ru.Value) ?? 0.0) >=
                    dto.ScoreLowerLimit);
        if (dto.SealOfQuality != null)
            query = query
                .Where(r => r.ResourceVersions.Any(rv => rv.SealOfQuality) == dto.SealOfQuality);
        if (dto.TagLike != null)
        {
            var escaped = dto.TagLike
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_")
                .Replace("[", @"\[");

            var pattern = $"%{escaped}%";
            query = query
                .Where(r => r.ResourceTags
                    .Any(rt => EF.Functions
                        .Like(rt.Tag!.Name, pattern, @"\")));
        }
        if (dto.TypeIds.Length > 0)
            query = query.Where(r =>
                r.ResourceResourceTypes.Any(t =>
                    dto.TypeIds.Contains(t.ResourceTypeId)));
        if (dto.VersionCreatedAtFrom is { } fromV)
        {
            var limit = fromV.Date;
            query = query
                .Where(r => r.ResourceVersions
                    .OrderByDescending(rv => rv.CreatedAt)
                    .ThenByDescending(rv => rv.Id)
                    .Select(rv => rv.CreatedAt)
                    .First()! >= limit);
        }
        if (dto.VersionCreatedAtTo is { } toV)
        {
            var limit = toV.Date.AddDays(1);
            query = query
                .Where(r => r.ResourceVersions
                    .OrderByDescending(rv => rv.CreatedAt)
                    .ThenByDescending(rv => rv.Id)
                    .Select(rv => rv.CreatedAt)
                    .First()! < limit);
        }
        if (dto.VideogameIDs.Length > 0)
            query = query.Where(r =>
                dto.VideogameIDs.Contains(r.VideogameId));
        return query;
    }
    private async Task<OneOf<List<ResourceFinderViewDTO>, ResourceServiceResult>> SelectFind(IQueryable<Resource> query, CancellationToken ct = default)
    {
        var result = await _dbHelperService.TryFunction(
            async cancel => await query
            .AsSplitQuery()
            .Select(r => new ResourceFinderViewDTO
            {
                ResourceIdName = new()
                {
                    Id = r.Id,
                    Name = r.Name
                },
                SubmitterUserSummary = new()
                {
                    Id = r.SubmitterId,
                    Username = r.Submitter!.UserName!,
                    UsernameColor = r.Submitter.UserInfo!.UsernameColor
                },
                AuthorsUserSummary = r.ResourcesAuthors
                    .Select(ra => new UserNameDTO
                    {
                        Id = ra.UserId,
                        Username = ra.User!.UserName!,
                        UsernameColor = ra.User.UserInfo!.UsernameColor
                    })
                    .ToArray(),
                ExternalAuthors = r.ResourceExternalAuthors
                    .Select(rea => new IdNamePair
                    {
                        Id = rea.Id,
                        Name = rea.Author
                    })
                    .ToArray(),
                Categories = r.ResourceResourceTypes
                    .Select(rrt => new IdNamePair
                    {
                        Id = rrt.ResourceType!.CategoryId,
                        Name = rrt.ResourceType!.Category!.Name,
                    })
                    .ToArray(),
                Types = r.ResourceResourceTypes
                    .Select(rrt => new IdNamePair
                    {
                        Id = rrt.ResourceTypeId,
                        Name = rrt.ResourceType!.Name!,
                    })
                    .ToArray(),
                Videogame = new IdNamePair
                    {
                        Id = r.VideogameId,
                        Name = r.Videogame!.Name!,
                    },
                IncludesGore = r.IncludesGore,
                IncludesPolitics = r.IncludesPolitics,
                IncludesPorn = r.IncludesPorn,
                IncludesSensitiveContent = r.IncludesSensitiveContent,
                IncludesSlurs = r.IncludesSlurs,
                Tags = r.ResourceTags
                    .Select(rt => new IdNamePair
                    {
                        Id = rt.TagId,
                        Name = rt.Tag!.Name,
                    })
                    .ToArray(),
                CreatedAt = r.CreatedAt,
                Downloads = r.ResourceVersions.Sum(rv => (long?)rv.Downloads) ?? 0L,
                MainVersion = r.ResourceVersions
                    .OrderByDescending(rv => rv.CreatedAt)
                    .ThenByDescending(rv => rv.Id)
                    .Select(rv => new FileResponse
                    {
                        FileTypeId = rv.File!.FileTypeId,
                        Id = rv.FileId,
                        URL = rv.File.URL
                    })
                    .First(),
                Score = r.Score!.ScoresUsers.Average(su => (double?)su.Value) ?? 0.0,
                Votes = r.Score.ScoresUsers.Count,
                SealOfQuality = r.ResourceVersions.Any(rv => rv.SealOfQuality),
                Version = r.ResourceVersions
                    .OrderByDescending(rv => rv.CreatedAt)
                    .ThenByDescending(rv => rv.Id)
                    .Select(rv => rv.Version)
                    .First()!,
                VersionCreatedAt = r.ResourceVersions
                    .OrderByDescending(rv => rv.CreatedAt)
                    .ThenByDescending(rv => rv.Id)
                    .Select(rv => rv.CreatedAt)
                    .First()!
            })
            .ToListAsync(cancel), ResourceServiceResult.DBError, ct: ct);

        if (!result.TryPickT0(out var resources, out var error))
            return error;

        foreach (var resource in resources)
        {
            resource.Categories = [.. resource.Categories
                .DistinctBy(category => category.Id)
                .OrderBy(category => category.Name)];
        }

        return resources;
    }
    private async Task<bool> ValidateVersion(long? resourceId, long? versionId, 
        string version, CancellationToken ct = default)
    {
        if (version.Length > MAX_VERSION_SIZE || !_version().IsMatch(version))
            return false;

        if (resourceId == null)
            return true;

        var versions = await _dbHelperService.TryFunction(
            cancel => _context.Resources
                .AsNoTracking()
                .Where(r => r.Id == resourceId)
                .Select(r => r.ResourceVersions
                    .OrderBy(v => v.CreatedAt)
                    .ThenBy(v => v.Id)
                    .Select(v => new { v.Id, v.Version })
                    .ToList())
                .FirstOrDefaultAsync(cancel),
             ct: ct);

        if (versions == null)
            return false;

        int position = versions.FindIndex(v => v.Id == versionId);

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

        return ( int.Parse(numbers[0], CultureInfo.InvariantCulture),
            int.Parse(numbers.ElementAtOrDefault(1) ?? "0", CultureInfo.InvariantCulture),
            int.Parse(numbers.ElementAtOrDefault(2) ?? "0", CultureInfo.InvariantCulture),
            stage,
            suffix.Length == 2 ? int.Parse(suffix[1], CultureInfo.InvariantCulture) : -1);
    }
    private static bool IsValidRepositoryUrl(string value)
    {
        return value.Length <= 2048 && RepositoryUrlRegex().IsMatch(value) &&
            Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            uri.IsWellFormedOriginalString() &&
            (uri.Scheme == Uri.UriSchemeHttps ||
             uri.Scheme == Uri.UriSchemeHttp) &&
            !string.IsNullOrWhiteSpace(uri.Host) &&
            string.IsNullOrEmpty(uri.UserInfo);
    }
    private static ResourceServiceResult ValidateFinder(ResourceFinderDTO dto)
    {
        if (dto.Sorting is null ||
            !Enum.IsDefined(dto.Sorting.SortType))
            return ResourceServiceResult.InvalidSort;

        if (dto.ScoreLowerLimit is < 1 or > 5 ||
            dto.ScoreHigherLimit is < 1 or > 5)
            return ResourceServiceResult.InvalidScore;

        if (dto.ScoreLowerLimit > dto.ScoreHigherLimit)
            return ResourceServiceResult.InvalidScoreRange;

        if (!IsValidDateRange(dto.CreatedAtFrom, dto.CreatedAtTo) ||
            !IsValidDateRange(
                dto.VersionCreatedAtFrom, dto.VersionCreatedAtTo))
            return ResourceServiceResult.InvalidDateRange;

        return ResourceServiceResult.Success;
    }
    private static bool IsValidDateRange(DateTime? from, DateTime? to)
    {
        // FilterFind calcula el día siguiente con AddDays(1).
        if (to?.Date == DateTime.MaxValue.Date)
            return false;

        return from is null ||
            to is null ||
            from.Value.Date <= to.Value.Date;
    }
    [GeneratedRegex(@"^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\.|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex _sanitizeFolderRegex();
    [GeneratedRegex(@"[<>:""/\\|?*\x00-\x1F]")]
    private static partial Regex _sanitizeFolderRegex2();
    [GeneratedRegex(@"\A[0-9]{1,4}(?:\.[0-9]{1,4}){0,2}(?:-(?:alpha|beta|rc)(?:\.[0-9]{1,4})?)?\z")]
    private static partial Regex _version();
    [GeneratedRegex(@"\Ahttps?://[^\s<>""\\]+\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RepositoryUrlRegex();
    [GeneratedRegex(@"\A[\p{L}\p{Nd}][\p{L}\p{Nd} ]{0,98}[\p{L}\p{Nd}]\z")]
    private static partial Regex ResourceNameRegex();
}
