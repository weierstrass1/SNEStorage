using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services.Interfaces;

namespace SNEStorage.Services;

public partial class ResourceService
{
    private async Task<OneOf<List<ResourceFinderViewDTO>, ServiceResultContext>> FullGetInternal(
        ResourceFinderDTO finder, ResourceSortingDTO? sort, int maxAmount = 20,
        int page = 0, CancellationToken ct = default)
    {
        var validation = ValidateFinder(finder, sort);
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
            .Where(resource => resource.VisibilityId == 1 || isModOrAdmin ||
                (userId != null && (resource.SubmitterId == userId ||
                resource.ResourcesAuthors.Any(author => author.UserId == userId) ||
                resource.ResourceVersions.Any(version => version.SubmitterUserId == userId))));

        var pagination = new PaginationRequest(maxAmount, MAX_AMOUNT_PER_PAGE, page);
        IFullGet<Resource, ResourceFinderDTO, ResourceSortingDTO, ResourceFinderViewDTO> fullGet = this;
        var result = await fullGet.FullGetResult(
            query, finder, sort!, pagination, _dbHelperService, ct: ct);

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

    public IQueryable<Resource> GetSort(IQueryable<Resource> query, ResourceSortingDTO sort)
    {
        var ordered = sort.SortType switch
        {
            ResourceSorting.BestScore => sort.SortOrderDescending ?
                query.OrderByDescending(resource => resource.Score!.ScoresUsers
                    .Average(score => (double?)score.Value) ?? 0.0) :
                query.OrderBy(resource => resource.Score!.ScoresUsers
                    .Average(score => (double?)score.Value) ?? 0.0),
            ResourceSorting.Downloads => sort.SortOrderDescending ?
                query.OrderByDescending(resource =>
                    resource.ResourceVersions.Sum(version => (long?)version.Downloads) ?? 0L) :
                query.OrderBy(resource =>
                    resource.ResourceVersions.Sum(version => (long?)version.Downloads) ?? 0L),
            ResourceSorting.Name => sort.SortOrderDescending ?
                query.OrderByDescending(resource => resource.Name) :
                query.OrderBy(resource => resource.Name),
            ResourceSorting.NumberOfVotes => sort.SortOrderDescending ?
                query.OrderByDescending(resource => resource.Score!.ScoresUsers.Count) :
                query.OrderBy(resource => resource.Score!.ScoresUsers.Count),
            ResourceSorting.VersionCreatedAt => sort.SortOrderDescending ?
                query.OrderByDescending(resource => resource.ResourceVersions
                    .OrderByDescending(version => version.CreatedAt)
                    .ThenByDescending(version => version.Id)
                    .Select(version => version.CreatedAt)
                    .First()!) :
                query.OrderBy(resource => resource.ResourceVersions
                    .OrderByDescending(version => version.CreatedAt)
                    .ThenByDescending(version => version.Id)
                    .Select(version => version.CreatedAt)
                    .First()!),
            _ => sort.SortOrderDescending ?
                query.OrderByDescending(resource => resource.CreatedAt) :
                query.OrderBy(resource => resource.CreatedAt)
        };

        return ordered.ThenByDescending(resource => resource.Id);
    }

    public IQueryable<Resource> GetFilter(IQueryable<Resource> query, ResourceFinderDTO dto)
    {
        string? userId = _accountService.GetUserID();
        var birthdayLimit = DateOnly
            .FromDateTime(DateTime.UtcNow)
            .AddYears(-18);

        query = query.Where(resource =>
            !(resource.IncludesPorn ||
              resource.IncludesGore ||
              resource.IncludesPolitics ||
              resource.IncludesSlurs ||
              resource.IncludesSensitiveContent) ||
            (userId != null && _context.UserInfos.Any(info =>
                info.UserId == userId &&
                info.Birthday > DateOnly.MinValue &&
                info.Birthday <= birthdayLimit)));

        if (dto.AuthorsIDs.Length > 0)
            query = query.Where(resource =>
                resource.ResourcesAuthors.Any(author => dto.AuthorsIDs.Contains(author.UserId)));
        if (!dto.CanIncludesGore)
            query = query.Where(resource => !resource.IncludesGore);
        if (!dto.CanIncludesPolitics)
            query = query.Where(resource => !resource.IncludesPolitics);
        if (!dto.CanIncludesPorn)
            query = query.Where(resource => !resource.IncludesPorn);
        if (!dto.CanIncludesSensitiveContent)
            query = query.Where(resource => !resource.IncludesSensitiveContent);
        if (!dto.CanIncludesSlurs)
            query = query.Where(resource => !resource.IncludesSlurs);
        if (dto.CategoryIDs.Length > 0)
            query = query.Where(resource => resource.ResourceResourceTypes.Any(type =>
                dto.CategoryIDs.Contains(type.ResourceType!.CategoryId)));
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
                    EF.Functions.Like(author.User!.UserName!, pattern, @"\")) ||
                resource.ResourceExternalAuthors.Any(author =>
                    EF.Functions.Like(author.Author, pattern, @"\")));
        }
        if (dto.CreatedAtFrom is { } from)
        {
            var limit = from.Date;
            query = query.Where(resource => resource.CreatedAt >= limit);
        }
        if (dto.CreatedAtTo is { } to)
        {
            var limit = to.Date.AddDays(1);
            query = query.Where(resource => resource.CreatedAt < limit);
        }
        if (dto.MustHaveScore != null)
            query = query.Where(resource =>
                resource.Score!.ScoresUsers.Any() == dto.MustHaveScore);
        if (dto.NameLike != null)
        {
            var escaped = dto.NameLike
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_")
                .Replace("[", @"\[");
            var pattern = $"%{escaped}%";
            query = query.Where(resource =>
                EF.Functions.Like(resource.Name, pattern, @"\"));
        }
        if (dto.ScoreHigherLimit != null)
            query = query.Where(resource =>
                (resource.Score!.ScoresUsers.Average(score => (double?)score.Value) ?? 0.0) <=
                dto.ScoreHigherLimit);
        if (dto.ScoreLowerLimit != null)
            query = query.Where(resource =>
                (resource.Score!.ScoresUsers.Average(score => (double?)score.Value) ?? 0.0) >=
                dto.ScoreLowerLimit);
        if (dto.SealOfQuality != null)
            query = query.Where(resource =>
                resource.ResourceVersions.Any(version => version.SealOfQuality) == dto.SealOfQuality);
        if (dto.TagLike != null)
        {
            var escaped = dto.TagLike
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_")
                .Replace("[", @"\[");
            var pattern = $"%{escaped}%";
            query = query.Where(resource => resource.ResourceTags.Any(resourceTag =>
                EF.Functions.Like(resourceTag.Tag!.Name, pattern, @"\")));
        }
        if (dto.TypeIds.Length > 0)
            query = query.Where(resource => resource.ResourceResourceTypes.Any(type =>
                dto.TypeIds.Contains(type.ResourceTypeId)));
        if (dto.VersionCreatedAtFrom is { } fromVersion)
        {
            var limit = fromVersion.Date;
            query = query.Where(resource => resource.ResourceVersions
                .OrderByDescending(version => version.CreatedAt)
                .ThenByDescending(version => version.Id)
                .Select(version => version.CreatedAt)
                .First()! >= limit);
        }
        if (dto.VersionCreatedAtTo is { } toVersion)
        {
            var limit = toVersion.Date.AddDays(1);
            query = query.Where(resource => resource.ResourceVersions
                .OrderByDescending(version => version.CreatedAt)
                .ThenByDescending(version => version.Id)
                .Select(version => version.CreatedAt)
                .First()! < limit);
        }
        if (dto.VideogameIDs.Length > 0)
            query = query.Where(resource => dto.VideogameIDs.Contains(resource.VideogameId));

        return query;
    }

    public IQueryable<ResourceFinderViewDTO> GetSelect(IQueryable<Resource> query)
    {
        return query.AsSplitQuery().Select(resource => new ResourceFinderViewDTO
        {
            ResourceIdName = new()
            {
                Id = resource.Id,
                Name = resource.Name
            },
            SubmitterUserSummary = new()
            {
                Id = resource.SubmitterId,
                Username = resource.Submitter!.UserName!,
                UsernameColor = resource.Submitter.UserInfo!.UsernameColor
            },
            AuthorsUserSummary = resource.ResourcesAuthors
                .Select(author => new UserNameDTO
                {
                    Id = author.UserId,
                    Username = author.User!.UserName!,
                    UsernameColor = author.User.UserInfo!.UsernameColor
                })
                .ToArray(),
            ExternalAuthors = resource.ResourceExternalAuthors
                .Select(author => new IdNamePair
                {
                    Id = author.Id,
                    Name = author.Author
                })
                .ToArray(),
            Categories = resource.ResourceResourceTypes
                .Select(type => new IdNamePair
                {
                    Id = type.ResourceType!.CategoryId,
                    Name = type.ResourceType.Category!.Name
                })
                .ToArray(),
            Types = resource.ResourceResourceTypes
                .Select(type => new IdNamePair
                {
                    Id = type.ResourceTypeId,
                    Name = type.ResourceType!.Name!
                })
                .ToArray(),
            Videogame = new IdNamePair
            {
                Id = resource.VideogameId,
                Name = resource.Videogame!.Name!
            },
            IncludesGore = resource.IncludesGore,
            IncludesPolitics = resource.IncludesPolitics,
            IncludesPorn = resource.IncludesPorn,
            IncludesSensitiveContent = resource.IncludesSensitiveContent,
            IncludesSlurs = resource.IncludesSlurs,
            Tags = resource.ResourceTags
                .Select(resourceTag => new IdNamePair
                {
                    Id = resourceTag.TagId,
                    Name = resourceTag.Tag!.Name
                })
                .ToArray(),
            CreatedAt = resource.CreatedAt,
            Downloads = resource.ResourceVersions.Sum(version => (long?)version.Downloads) ?? 0L,
            MainVersion = resource.ResourceVersions
                .OrderByDescending(version => version.CreatedAt)
                .ThenByDescending(version => version.Id)
                .Select(version => new FileResponse
                {
                    FileTypeId = version.File!.FileTypeId,
                    Id = version.FileId,
                    URL = version.File.URL
                })
                .First(),
            Score = resource.Score!.ScoresUsers.Average(score => (double?)score.Value) ?? 0.0,
            Votes = resource.Score.ScoresUsers.Count,
            SealOfQuality = resource.ResourceVersions.Any(version => version.SealOfQuality),
            Version = resource.ResourceVersions
                .OrderByDescending(version => version.CreatedAt)
                .ThenByDescending(version => version.Id)
                .Select(version => version.Version)
                .First()!,
            VersionCreatedAt = resource.ResourceVersions
                .OrderByDescending(version => version.CreatedAt)
                .ThenByDescending(version => version.Id)
                .Select(version => version.CreatedAt)
                .First()!
        });
    }

    private static ServiceResult ValidateFinder(ResourceFinderDTO dto, ResourceSortingDTO? sort)
    {
        if (dto.NameLike?.Length > TextLimits.RESOURCE_NAME_SEARCH ||
            dto.AuthorLike?.Length > TextLimits.SEARCH || dto.TagLike?.Length > TextLimits.TAG)
            return ServiceResult.InvalidSearch;
        if (dto.AuthorsIDs.Any(id => id is null || id.Length > TextLimits.USER_ID))
            return ServiceResult.InvalidUserId;

        if (sort is null || !Enum.IsDefined(sort.SortType))
            return ServiceResult.InvalidSort;
        if (dto.ScoreLowerLimit is < 1 or > 5 ||
            dto.ScoreHigherLimit is < 1 or > 5)
            return ServiceResult.InvalidScore;
        if (dto.ScoreLowerLimit > dto.ScoreHigherLimit)
            return ServiceResult.InvalidScoreRange;
        if (!IsValidDateRange(dto.CreatedAtFrom, dto.CreatedAtTo) ||
            !IsValidDateRange(dto.VersionCreatedAtFrom, dto.VersionCreatedAtTo))
            return ServiceResult.InvalidDateRange;

        return ServiceResult.Success;
    }

    private static bool IsValidDateRange(DateTime? from, DateTime? to)
    {
        if (to?.Date == DateTime.MaxValue.Date)
            return false;

        return from is null || to is null || from.Value.Date <= to.Value.Date;
    }
}
