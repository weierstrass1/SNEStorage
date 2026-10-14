using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace SNEStorage.Services;

public partial class ResourceService(SnestorageContext context, CacheService cacheService,
    FileService fileService, AccountService accountService, DBHelperService dbHelperService,
    DiscordService discordService, ResourceAccessService accessService,
    ResourceVersionService versionService, ILogger<ResourceService> logger)
{
    public const int MIN_NAME_SIZE = 2;
    public const int MAX_NAME_SIZE = 100;
    public const int MIN_DESCRIPTION_SIZE = 10;
    public const int MAX_DESCRIPTION_SIZE = 10000;
    public const int MAX_AMOUNT_OF_ELEMENTS = 50;
    public const int MAX_AMOUNT_LIKE_LIMIT = 20;
    public const long FILE_MAX_SIZE = 250 * 1024 * 1024;
    public const long PREVIEW_MAX_SIZE = 5 * 1024 * 1024;
    public const int MAX_AMOUNT_OF_PREVIEWS = 20;
    public const int MAX_AMOUNT_PER_PAGE = 50;
    private readonly SnestorageContext _context = context;
    private readonly CacheService _cacheService = cacheService;
    private readonly FileService _fileService = fileService;
    private readonly AccountService _accountService = accountService;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly DiscordService _discordService = discordService;
    private readonly ResourceAccessService _accessService = accessService;
    private readonly ResourceVersionService _versionService = versionService;
    private readonly ILogger<ResourceService> _logger = logger;

    public async Task<OneOf<List<ResourceFinderViewDTO>, ServiceResultContext>> Find(
        ResourceFinderDTO finder, int maxAmount = 20, int page = 0, CancellationToken ct = default)
    {
        var validation = ValidateFinder(finder);
        if (validation != ServiceResult.Success)
            return validation.Return(_logger);
        if (maxAmount <= 0)
            return ServiceResult.InvalidMaxAmount.Return(_logger);
        if (maxAmount > MAX_AMOUNT_PER_PAGE)
            return ServiceResult.MaxAmountSurpassTheLimit.Return(_logger);
        if (page < 0)
            return ServiceResult.NegativePage.Return(_logger);
        long offset = (long)maxAmount * page;
        if (offset > int.MaxValue)
            return ServiceResult.SkipOutOfRange.Return(_logger);

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
    public async Task<OneOf<List<Resource>, ServiceResultContext>> GetResourceLike(
        string likeString, int maxAmount = 10, CancellationToken ct = default)
    {
        if (maxAmount <= 0)
            return ServiceResult.InvalidMaxAmount.Return(_logger);
        if (maxAmount > MAX_AMOUNT_LIKE_LIMIT)
            return ServiceResult.MaxAmountSurpassTheLimit.Return(_logger);

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
            cancel => query.OrderBy(v => v.Name).Take(maxAmount).ToListAsync(cancel),
            ServiceResult.DBError, ct: ct);
    }

    public async Task<ServiceResultContext> Edit(long resourceId, ResourceEditDTO dto, CancellationToken ct = default)
    {
        if (dto.Name != null)
        {
            dto.Name = dto.Name.Trim();
            if (string.IsNullOrWhiteSpace(dto.Name))
                return ServiceResult.EmptyName.Return(_logger);
            if (dto.Name.Length < MIN_NAME_SIZE || dto.Name.Length > MAX_NAME_SIZE ||
                !ResourceNameRegex().IsMatch(dto.Name))
                return ServiceResult.InvalidName.Return(_logger);
        }
        if (dto.Description != null)
        {
            dto.Description = dto.Description.Trim();
            if (string.IsNullOrWhiteSpace(dto.Description))
                return ServiceResult.EmptyDescription.Return(_logger);
            if (dto.Description.Length < MIN_DESCRIPTION_SIZE ||
                dto.Description.Length > MAX_DESCRIPTION_SIZE)
                return ServiceResult.InvalidDescription.Return(_logger);
        }
        if (dto.RepositoryURL != null)
        {
            dto.RepositoryURL = dto.RepositoryURL.Trim();
            if (dto.RepositoryURL.Length > 0 &&
                !IsValidRepositoryUrl(dto.RepositoryURL))
                return ServiceResult.InvalidRepositoryURL.Return(_logger);
        }
        if (dto.Previews.Any(pr => pr.Length > PREVIEW_MAX_SIZE))
            return ServiceResult.PreviewSizeSurpassTheLimit.Return(_logger);
        var accessResult = await _accessService.GetEditAccess(resourceId, ct);
        if (!accessResult.TryPickT0(out _, out var accessError))
            return accessError;

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
            ServiceResult.DBError, ct: ct);

        if (!res.TryPickT0(out var resource, out var error))
            return error;
        if (resource == null)
            return ServiceResult.ResourceNotFound.Return(_logger);

        var userId = _accountService.GetUserID();
        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult.UserNotFoundBadRequest.Return(_logger);

        string mainFolder = ResourcePathHelper.GetMainResourceFolder(resource);
        var batchResult = await _fileService.BeginBatch(ct);
        if (!batchResult.TryPickT0(out var batchfile, out var batchError))
            return batchError;

        var prevResult = await GetPreviews(dto.Previews, mainFolder, batchfile, ct);
        if (!prevResult.TryPickT0(out var resourcePreviews, out var previewError))
            return previewError;

        var tagRes = await GetTags(dto.Tags, ct);
        if (!tagRes.TryPickT0(out var tags, out var tagError))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return tagError;
        }

        var typesRes = await GetTypes(dto.Types, ct);
        if (!typesRes.TryPickT0(out var types, out var typeError))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return typeError;
        }

        if (types.Any(t => t == null))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return ServiceResult.ResourceTypeNotFound.Return(_logger);
        }

        var authorRes = await GetAuthors(dto.AuthorsIDs, dto.IAmAuthor == true, userId, ct);
        if (!authorRes.TryPickT0(out var authors, out var authorError))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return authorError;
        }

        if (authors.Any(t => t == null))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return ServiceResult.UserNotFoundBadRequest.Return(_logger);
        }

        List<ResourceExternalAuthor> externalAuthors = GetExternalAuthors(dto.ExternalAuthors);

        var dependenciesRes = await GetDependencies(dto.DependencyIDs, ct);
        if (!dependenciesRes.TryPickT0(out var dependencies, out var dependenciesError))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return dependenciesError;
        }

        if (dependencies.Any(t => t == null))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return ServiceResult.ResourceNotFound.Return(_logger);
        }

        List<ResourceExternalDependency> externalDependencies = GetExternalDependencies(dto.ExternalDependencies);

        resource.ResourceResourceTypes = [..resource.ResourceResourceTypes
            .Where(d => !dto.RemovedTypeIDs.Contains(d.Id))
            .Concat(types)
            .DistinctBy(d =>d!.ResourceTypeId)!];
        if (resource.ResourceResourceTypes.Count <= 0)
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ServiceResult.EmptyTypeList.Return(_logger);
        }

        resource.ResourcePreviews = [..resource.ResourcePreviews
            .Where(d => !dto.RemovePreviewIDs.Contains(d.Id))
            .Concat(resourcePreviews.Where(t => t.FileId != 0))
            .DistinctBy(d =>d.FileId)
            .Concat(resourcePreviews.Where(t => t.FileId == 0))];

        if (resource.ResourcePreviews.Count > MAX_AMOUNT_OF_PREVIEWS)
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ServiceResult.PreviewAmountSurpassTheLimit.Return(_logger);
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
            return ServiceResult.PreviewAmountSurpassTheLimit.Return(_logger);
        }

        if (resource.ResourcesAuthors.Count + resource.ResourceExternalAuthors.Count > MAX_AMOUNT_OF_ELEMENTS ||
            resource.ResourceDependencies.Count + resource.ResourceExternalDependencies.Count > MAX_AMOUNT_OF_ELEMENTS ||
            resource.ResourceTags.Count > MAX_AMOUNT_OF_ELEMENTS)
        {
            await _fileService.BatchRollback(batchfile, ct);
            return ServiceResult.AmountOfElementsSurpassTheLimit.Return(_logger);
        }

        var removeResult = await _dbHelperService.TryFunction(
            cancel => _context.ResourcePreviews.AsNoTracking()
                .Where(preview => preview.ResourceId == resourceId &&
                    prIds.Contains(preview.Id))
                .ToListAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (!removeResult.TryPickT0(out var removePreviews, out var removeError))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return removeError;
        }

        var filesResult = await _fileService.PrepareDeletion(
            removePreviews.Select(preview => preview.FileId), ct);

        if (!filesResult.TryPickT0(out var files, out var filesError))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return filesError;
        }

        var saveResult = await _fileService.TrySyncContextWithFiles(batchfile, ct);

        if (saveResult.Result != ServiceResult.Success)
            return saveResult;

        _ = _fileService.FinishDeletion(files);
        return ServiceResult.Success.Return(_logger);
    }

    public async Task<OneOf<ResourceReadDTO, ServiceResultContext>> Get(long resourceId, CancellationToken ct = default)
    {
        var accessResult = await _accessService.GetViewAccess(resourceId, ct);
        if (!accessResult.TryPickT0(out var access, out var accessError))
            return accessError;

        bool canEdit = access.IsContributor;

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
            ServiceResult.DBError,
            ct: ct);

        return ItemOrError(result);
    }

    public async Task<OneOf<Resource, ServiceResultContext>> Create(ResourceCreateDTO dto, CancellationToken ct = default)
    {
        dto.Name = dto.Name.Trim();
        dto.Description = dto.Description.Trim();
        dto.RepositoryURL = dto.RepositoryURL?.Trim();
        dto.ChangeLog = dto.ChangeLog.Trim();

        if (string.IsNullOrWhiteSpace(dto.Name))
            return ServiceResult.EmptyName.Return(_logger);
        if (dto.Name.Length < MIN_NAME_SIZE || dto.Name.Length > MAX_NAME_SIZE ||
            !ResourceNameRegex().IsMatch(dto.Name))
            return ServiceResult.InvalidName.Return(_logger);
        if (string.IsNullOrWhiteSpace(dto.Description))
            return ServiceResult.EmptyDescription.Return(_logger);
        if (dto.Description.Length < MIN_DESCRIPTION_SIZE || dto.Description.Length > MAX_DESCRIPTION_SIZE)
            return ServiceResult.InvalidDescription.Return(_logger);
        if (dto.ChangeLog.Length < MIN_DESCRIPTION_SIZE || dto.ChangeLog.Length > MAX_DESCRIPTION_SIZE)
            return ServiceResult.InvalidReleaseNote.Return(_logger);
        if (dto.RepositoryURL != null && !IsValidRepositoryUrl(dto.RepositoryURL))
            return ServiceResult.InvalidRepositoryURL.Return(_logger);
        if (dto.File.Length > FILE_MAX_SIZE)
            return ServiceResult.FileSizeSurpassTheLimit.Return(_logger);
        if (dto.Previews.Any(pr => pr.Length > PREVIEW_MAX_SIZE))
            return ServiceResult.PreviewSizeSurpassTheLimit.Return(_logger);
        if (dto.Previews.Length > MAX_AMOUNT_OF_PREVIEWS)
            return ServiceResult.PreviewAmountSurpassTheLimit.Return(_logger);
        if (dto.Types is null || dto.Types.Length == 0)
            return ServiceResult.EmptyTypeList.Return(_logger);
        if (dto.AuthorsIDs.Length + dto.ExternalAuthors.Length > MAX_AMOUNT_OF_ELEMENTS ||
            dto.DependencyIDs.Length + dto.ExternalDependencies.Length > MAX_AMOUNT_OF_ELEMENTS ||
            dto.Tags.Length > MAX_AMOUNT_OF_ELEMENTS)
            return ServiceResult.AmountOfElementsSurpassTheLimit.Return(_logger);

        var userId = _accountService.GetUserID();
        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult.UserNotFoundBadRequest.Return(_logger);

        var versionValidation = await _versionService.ValidateInitialVersion(dto, ct);
        if (versionValidation.Result != ServiceResult.Success)
            return versionValidation;

        string name = ResourcePathHelper.SanitizeFolderName(dto.Name);

        var mainFolder = Path.Combine("Storage", "Resources",
            $"{Guid.NewGuid():N}_{name}");
        var batchResult = await _fileService.BeginBatch(ct);
        if (!batchResult.TryPickT0(out var batchfile, out var batchError))
            return batchError;

        var versionResult = await _versionService.PrepareInitialVersion(
            dto, userId, mainFolder, batchfile, ct);
        if (!versionResult.TryPickT0(out var resourceVersion, out var versionError))
            return versionError;

        var prevResult = await GetPreviews(dto.Previews, mainFolder, batchfile, ct);
        if (!prevResult.TryPickT0(out var resourcePreviews, out var previewError))
            return previewError;

        var tagRes = await GetTags(dto.Tags, ct);
        if (!tagRes.TryPickT0(out var tags, out var tagError))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return tagError;
        }

        var typesRes = await GetTypes(dto.Types, ct);
        if (!typesRes.TryPickT0(out var types, out var typeError))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return typeError;
        }

        if (types.Any(t => t == null))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return ServiceResult.ResourceTypeNotFound.Return(_logger);
        }

        var authorRes = await GetAuthors(dto.AuthorsIDs, dto.IAmAuthor, userId, ct);
        if (!authorRes.TryPickT0(out var authors, out var authorError))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return authorError;
        }

        if (authors.Any(t => t == null))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return ServiceResult.UserNotFoundBadRequest.Return(_logger);
        }

        List<ResourceExternalAuthor> externalAuthors = GetExternalAuthors(dto.ExternalAuthors);

        var dependenciesRes = await GetDependencies(dto.DependencyIDs, ct);
        if (!dependenciesRes.TryPickT0(out var dependencies, out var dependenciesError))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return dependenciesError;
        }

        if (dependencies.Any(t => t == null))
        {
            _ = await _fileService.BatchRollback(batchfile, ct);
            return ServiceResult.ResourceNotFound.Return(_logger);
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

        var result = await _fileService.TrySyncContextWithFiles(batchfile, res, ct);

        if (!result.TryPickT0(out var createdResource, out var saveError))
            return saveError;

        await _discordService.NotifyBotAsync(new DiscordBotEventDTO(Guid.NewGuid(),
            DiscordBotEventTypes.ResourceCreated, createdResource.Id.ToString(CultureInfo.InvariantCulture),
            DateTimeOffset.UtcNow), ct);

        return createdResource;
    }

    private async Task<OneOf<List<ResourcePreview>, ServiceResultContext>> GetPreviews(
        IFormFile[] formFiles, string mainFolder, string batchPath, CancellationToken ct)
    {
        List<Models.File> previews = [];

        foreach (var preview in formFiles)
        {
            var previewPath = Path.Combine(mainFolder, $"Previews",
                $"{Guid.NewGuid():N}{Path.GetFileName(preview.FileName)}");
            var previewFileRes = await _fileService.PrepareFile(FileContext.Previews,
                preview, previewPath, batchPath, ct: ct);
            if (!previewFileRes.TryPickT0(out var previewFile, out var error))
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

    private async Task<OneOf<List<ResourceResourceTypes?>, ServiceResultContext>> GetTypes(long[] typeIds, CancellationToken ct)
    {
        List<ResourceResourceTypes?> types = [];

        var tRes = await _cacheService.GetAllTypes(ct);
        if (!tRes.TryPickT0(out var cachedTypes, out var cacheError))
            return cacheError;

        foreach (var typeid in typeIds.Distinct())
        {
            cachedTypes.TryGetValue(typeid, out var type);

            types.Add(type != null ?
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

    private async Task<OneOf<List<ResourceDependency?>, ServiceResultContext>> GetDependencies(long[] dependencyIds,
        CancellationToken ct)
    {
        var ids = dependencyIds.Distinct().ToArray();
        if (ids.Length == 0)
            return new List<ResourceDependency?>();

        var existingIds = await _dbHelperService.TryFunction(
            cancel => _context.Resources
                .AsNoTracking()
                .Where(resource => ids.Contains(resource.Id))
                .Select(resource => resource.Id)
                .ToListAsync(cancel), ct: ct);
        if (!existingIds.TryPickT0(out var foundIds, out var queryError))
            return queryError;

        var existing = foundIds.ToHashSet();
        List<ResourceDependency?> dependencies = [];

        foreach (var id in ids)
        {
            dependencies.Add(existing.Contains(id) ?
                new ResourceDependency
                {
                    Id = 0,
                    ResourceId = 0,
                    DependencyResourceId = id
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
    private async Task<OneOf<List<ResourceAuthors?>, ServiceResultContext>> GetAuthors(string[] authorIDs,
        bool iAmAuthor, string userId, CancellationToken ct)
    {
        var ids = authorIDs
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (iAmAuthor && !ids.Contains(userId, StringComparer.OrdinalIgnoreCase))
            ids.Add(userId);

        if (ids.Count == 0)
            return new List<ResourceAuthors?>();

        var users = await _dbHelperService.TryFunction(
            cancel => _context.Users.AsTracking()
                .Where(user => ids.Contains(user.Id))
                .ToListAsync(cancel), ct: ct);

        if (!users.TryPickT0(out var foundUsers, out var queryError))
            return queryError;

        var usersById = foundUsers.ToDictionary(user => user.Id,
            StringComparer.OrdinalIgnoreCase);
        List<ResourceAuthors?> authors = [];

        foreach (var id in ids)
        {
            authors.Add(usersById.TryGetValue(id, out var user) ?
                new ResourceAuthors
                {
                    ResourceId = 0,
                    UserId = user.Id,
                    User = user
                } :
                null);
        }

        return authors;
    }
    private async Task<OneOf<List<ResourceTags>, ServiceResultContext>> GetTags(string[] tagNames, CancellationToken ct)
    {
        var names = tagNames
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name.ToLower().Trim())
            .Distinct()
            .ToArray();

        if (names.Length == 0)
            return new List<ResourceTags>();

        var existingTags = await _dbHelperService.TryFunction(
            cancel => _context.Tags
                .AsTracking()
                .Where(tag => names.Contains(tag.Name.ToLower().Trim()))
                .OrderBy(tag => tag.Id)
                .ToListAsync(cancel), ct: ct);

        if (!existingTags.TryPickT0(out var foundTags, out var queryError))
            return queryError;

        var tagsByName = foundTags
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

    public async Task<OneOf<List<ResourceDependencyViewDTO>, ServiceResultContext>>
        GetResourceAllDependencies(long resourceId, CancellationToken ct)
    {
        var accessResult = await _accessService.GetViewAccess(resourceId, ct);
        if (!accessResult.TryPickT0(out _, out var accessError))
            return accessError;

        var res = await _dbHelperService.TryFunction(
            cancel => _context.ResourceDependencies
                .AsNoTracking()
                .Where(resource => resource.ResourceId == resourceId)
                .OrderByDescending(resource => resource.DependencyResource!.Name)
                .ThenBy(resource => resource.Id)
                .Select(resource => new ResourceDependencyViewDTO(resource.Id,
                    resource.DependencyResourceId, resource.DependencyResource!.Name))
                .ToListAsync(cancel),
            ServiceResult.DBError, ct: ct);
        return res;
    }
    public async Task<OneOf<List<ResourceExternalDependency>, ServiceResultContext>>
        GetResourceAllExternalDependencies(long resourceId, CancellationToken ct)
    {
        var accessResult = await _accessService.GetViewAccess(resourceId, ct);
        if (!accessResult.TryPickT0(out _, out var accessError))
            return accessError;

        var res = await _dbHelperService.TryFunction(
            cancel => _context.ResourceExternalDependencies
                .AsNoTracking()
                .Where(resource => resource.ResourceId == resourceId)
                .OrderByDescending(resource => resource.DependencyName)
                .ThenBy(resource => resource.Id)
                .ToListAsync(cancel),
            ServiceResult.DBError, ct: ct);
        return res;
    }
    public async Task<OneOf<List<ResourceExternalAuthor>, ServiceResultContext>>
        GetResourceAllExternalAuthors(long resourceId, CancellationToken ct)
    {
        var accessResult = await _accessService.GetViewAccess(resourceId, ct);
        if (!accessResult.TryPickT0(out _, out var accessError))
            return accessError;

        var res = await _dbHelperService.TryFunction(
            cancel => _context.ResourcesExternalAuthors
                .AsNoTracking()
                .Where(author => author.ResourceId == resourceId)
                .OrderByDescending(author => author.Author)
                .ThenBy(author => author.Id)
                .ToListAsync(cancel),
            ServiceResult.DBError, ct: ct);
        return res;
    }
    public async Task<OneOf<List<ResourceAuthorDTO>, ServiceResultContext>> GetResourceAllAuthors(long resourceId, CancellationToken ct)
    {
        var accessResult = await _accessService.GetViewAccess(resourceId, ct);
        if (!accessResult.TryPickT0(out _, out var accessError))
            return accessError;

        var res = await _dbHelperService.TryFunction(
            cancel => _context.ResourcesAuthors
                .AsNoTracking()
                .Where(author => author.ResourceId == resourceId)
                .Select(author => new ResourceAuthorDTO
                {
                    Id = author.Id,
                    UserSummary = new UserNameDTO
                    {
                        Id = author.UserId,
                        Username = author.User!.UserName!,
                        UsernameColor = author.User.UserInfo!.UsernameColor,
                        Avatar = new FileResponse
                        {
                            Id = author.User.UserInfo.Avatar!.Id,
                            FileTypeId = author.User.UserInfo.Avatar.FileTypeId,
                            URL = author.User.UserInfo.Avatar.URL
                        }
                    }
                })
                .OrderByDescending(author => author.UserSummary.Username)
                .ThenBy(author => author.Id)
                .ToListAsync(cancel),
            ServiceResult.DBError, ct: ct);
        return res;
    }

    public async Task<OneOf<List<ResourcePreview>, ServiceResultContext>> GetResourceAllPreviews(long resourceId, CancellationToken ct)
    {
        var accessResult = await _accessService.GetViewAccess(resourceId, ct);
        if (!accessResult.TryPickT0(out _, out var accessError))
            return accessError;

        var res = await _dbHelperService.TryFunction(
            cancel => _context.ResourcePreviews
                .AsNoTracking()
                .Where(preview => preview.ResourceId == resourceId)
                .Include(preview => preview.File)
                .OrderBy(preview => preview.Id)
                .ToListAsync(cancel),
            ServiceResult.DBError, ct: ct);
        return res;
    }

    private OneOf<T, ServiceResultContext> ItemOrError<T>(
        OneOf<T?, ServiceResultContext> item,
        ServiceResult? notFoundError = null) where T : class
    {
        if (!item.TryPickT0(out var i, out var error))
            return error;
        return i != null ? i :
            (notFoundError ?? ServiceResult.ResourceNotFound).Return(_logger);
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
    private async Task<OneOf<List<ResourceFinderViewDTO>, ServiceResultContext>> SelectFind(
        IQueryable<Resource> query, CancellationToken ct = default)
    {
        var result = await _dbHelperService.TryFunction(
            cancel => query
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
            .ToListAsync(cancel), ServiceResult.DBError, ct: ct);

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

    private static ServiceResult ValidateFinder(ResourceFinderDTO dto)
    {
        if (dto.Sorting is null ||
            !Enum.IsDefined(dto.Sorting.SortType))
            return ServiceResult.InvalidSort;

        if (dto.ScoreLowerLimit is < 1 or > 5 ||
            dto.ScoreHigherLimit is < 1 or > 5)
            return ServiceResult.InvalidScore;

        if (dto.ScoreLowerLimit > dto.ScoreHigherLimit)
            return ServiceResult.InvalidScoreRange;

        if (!IsValidDateRange(dto.CreatedAtFrom, dto.CreatedAtTo) ||
            !IsValidDateRange(
                dto.VersionCreatedAtFrom, dto.VersionCreatedAtTo))
            return ServiceResult.InvalidDateRange;

        return ServiceResult.Success;
    }
    private static bool IsValidDateRange(DateTime? from, DateTime? to)
    {
        if (to?.Date == DateTime.MaxValue.Date)
            return false;

        return from is null ||
            to is null ||
            from.Value.Date <= to.Value.Date;
    }

    [GeneratedRegex(@"\Ahttps?://[^\s<>""\\]+\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RepositoryUrlRegex();

    [GeneratedRegex(@"\A[\p{L}\p{Nd}][\p{L}\p{Nd} ]{0,98}[\p{L}\p{Nd}]\z")]
    private static partial Regex ResourceNameRegex();
}
