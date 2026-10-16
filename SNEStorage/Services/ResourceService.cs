using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.DTOs.QueryOperations;
using SNEStorage.Models;
using SNEStorage.Services.Interfaces;
using SNEStorage.Services.QueryComponents;
using System.Text.RegularExpressions;

namespace SNEStorage.Services;

public partial class ResourceService(SnestorageContext context, CacheService cacheService,
    FileService fileService, AccountService accountService, DBHelperService dbHelperService,
    DiscordService discordService, ResourceAccessService accessService,
    ResourceVersionService versionService,
    GetLikeBySelectorQueryOperation<Resource, Resource> getResourceLike,
    GetFirstOrErrorQueryOperation<Resource> getResource,
    GetFirstOrErrorQueryOperation<ResourceReadDTO> getResourceRead,
    GetListQueryOperation<ResourcePreview> getPreviewList,
    ILogger<ResourceService> logger) :
    IFullGet<Resource, ResourceFinderDTO, ResourceSortingDTO, ResourceFinderViewDTO>,
    IEditSingle<long, ResourceEditDTO, Resource>,
    ICreate<ResourceCreateDTO, Resource>
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
    private readonly GetLikeBySelectorQueryOperation<Resource, Resource>
        _getResourceLike = getResourceLike;
    private readonly GetFirstOrErrorQueryOperation<Resource> _getResource = getResource;
    private readonly GetFirstOrErrorQueryOperation<ResourceReadDTO>
        _getResourceRead = getResourceRead;
    private readonly GetListQueryOperation<ResourcePreview>
        _getPreviewList = getPreviewList;
    private readonly ILogger<ResourceService> _logger = logger;

    public Task<OneOf<List<ResourceFinderViewDTO>, ServiceResultContext>> Find(
        ResourceFinderDTO finder, int maxAmount = 20, int page = 0, CancellationToken ct = default)
    {
        return FullGet(finder, finder.Sorting, maxAmount, page, ct);
    }

    public Task<OneOf<List<ResourceFinderViewDTO>, ServiceResultContext>> FullGet(
        ResourceFinderDTO filter, ResourceSortingDTO sort, int maxAmount = 20,
        int page = 0, CancellationToken ct = default)
    {
        return FullGetInternal(filter, sort, maxAmount, page, ct);
    }

    public Task<OneOf<List<Resource>, ServiceResultContext>> GetResourceLike(
        string likeString, int maxAmount = 10, CancellationToken ct = default)
    {
        bool isStaff = _accountService.IsModeratorOrAdmin();
        string? userId = _accountService.GetUserID();

        var query = _context.Resources.AsNoTracking()
            .Where(resource => resource.VisibilityId != 3 || isStaff ||
                (userId != null &&
                (resource.SubmitterId == userId ||
                resource.ResourcesAuthors.Any(author => author.UserId == userId) ||
                resource.ResourceVersions.Any(version =>
                    version.SubmitterUserId == userId))));
        query = GetFilter(query, new ResourceFinderDTO
        {
            CanIncludesPorn = true,
            CanIncludesGore = true,
            CanIncludesPolitics = true,
            CanIncludesSlurs = true,
            CanIncludesSensitiveContent = true
        });

        var parameter = new GetLikeBySelectorQuery<Resource, Resource>(
            likeString, maxAmount, MAX_AMOUNT_LIKE_LIMIT,
            resource => resource.Name, resource => resource);

        return _getResourceLike.Execute(query, parameter, ct: ct);
    }

    public async Task<ServiceResultContext> Edit(
        long resourceId, ResourceEditDTO dto, CancellationToken ct = default)
    {
        IEditSingle<long, ResourceEditDTO, Resource> editor = this;
        var result = await editor.EditSingle(resourceId, dto, ct);

        return result.TryPickT0(out _, out var editError) ?
            ServiceResult.Success.Return(_logger) :
            editError;
    }

    Task<OneOf<Resource, ServiceResultContext>>
        IEditSingle<long, ResourceEditDTO, Resource>.Edit(
            long key, ResourceEditDTO edit, CancellationToken ct)
    {
        IEditSingle<long, ResourceEditDTO, Resource> editor = this;
        return editor.EditSingle(key, edit, ct);
    }

    public async Task<OneOf<ResourceReadDTO, ServiceResultContext>> Get(long resourceId, CancellationToken ct = default)
    {
        var accessResult = await _accessService.GetViewAccess(resourceId, ct);
        if (!accessResult.TryPickT0(out var access, out var accessError))
            return accessError;

        bool canEdit = access.IsContributor;

        var query = _context.Resources
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
            .AsSplitQuery();

        return await _getResourceRead.Execute(
            query, ServiceResult.ResourceNotFound, ct: ct);
    }

    public Task<OneOf<Resource, ServiceResultContext>> Create(
        ResourceCreateDTO dto, CancellationToken ct = default)
    {
        ICreate<ResourceCreateDTO, Resource> creator = this;
        return creator.CreateItem(dto, ct);
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
        if (authorIDs.Any(id => id is null || id.Length > TextLimits.USER_ID))
            return ServiceResult.InvalidUserId.Return(_logger);

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
        if (tagNames.Any(name => name is null || name.Length > TextLimits.TAG))
            return ServiceResult.InvalidTag.Return(_logger);

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
        string? userId = _accountService.GetUserID();
        bool isStaff = _accountService.IsModeratorOrAdmin();

        var availableResources = _context.Resources.AsNoTracking()
            .Where(resource => resource.VisibilityId != 3 || isStaff ||
                (userId != null &&
                (resource.SubmitterId == userId ||
                resource.ResourcesAuthors.Any(author =>
                    author.UserId == userId) ||
                resource.ResourceVersions.Any(version =>
                    version.SubmitterUserId == userId))));

        availableResources = GetFilter(availableResources, new ResourceFinderDTO
        {
            CanIncludesPorn = true,
            CanIncludesGore = true,
            CanIncludesPolitics = true,
            CanIncludesSlurs = true,
            CanIncludesSensitiveContent = true
        });

        return await _dbHelperService.TryFunction(
            cancel => _context.ResourceDependencies
                .AsNoTracking()
                .Where(dependency => dependency.ResourceId == resourceId)
                .OrderBy(dependency => dependency.Id)
                .Select(dependency => new ResourceDependencyViewDTO(dependency.Id,
                    dependency.DependencyResourceId, availableResources
                        .Where(resource =>
                            resource.Id == dependency.DependencyResourceId)
                        .Select(resource => resource.Name)
                        .FirstOrDefault()))
                .ToListAsync(cancel),
            ServiceResult.DBError, ct: ct);
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

    public async Task<OneOf<List<ResourcePreview>, ServiceResultContext>> GetResourceAllPreviews(
        long resourceId, CancellationToken ct)
    {
        var accessResult = await _accessService.GetViewAccess(resourceId, ct);
        if (!accessResult.TryPickT0(out _, out var accessError))
            return accessError;

        var query = _context.ResourcePreviews
            .AsNoTracking()
            .Where(preview => preview.ResourceId == resourceId)
            .Include(preview => preview.File)
            .OrderBy(preview => preview.Id);

        return await _getPreviewList.Execute(query, ct: ct);
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


    [GeneratedRegex(@"\Ahttps?://[^\s<>""\\]+\z", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex RepositoryUrlRegex();

    [GeneratedRegex(@"\A[\p{L}\p{Nd}][\p{L}\p{Nd} ]{0,98}[\p{L}\p{Nd}]\z")]
    private static partial Regex ResourceNameRegex();
}
