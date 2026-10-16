using System.ComponentModel.DataAnnotations;
namespace SNEStorage.DTOs;

public enum ResourceSorting
{
    CreatedAt,
    NumberOfVotes,
    BestScore,
    Name,
    VersionCreatedAt,
    Downloads
}

public sealed record RatingReadDto(double Average, int VoteCount, long? MyRating);
public sealed record ResourceAccessRow(long Id, long ScoreId, long VisibilityId, 
    bool IsContributor, bool ContentAllowed);
public sealed record ResourceDependencyViewDTO(
    long Id,
    long DependencyResourceId,
    string? Name);
public sealed class ResourceAuthorDTO
{
    public long Id { get; set; }
    public required UserNameDTO UserSummary { get; set; }
}
public sealed class ResourceSortingDTO
{
    [EnumDataType(typeof(ResourceSorting))]
    public ResourceSorting SortType { get; set; } = ResourceSorting.CreatedAt;
    public bool SortOrderDescending { get; set; } = true;
}
public sealed class ResourceFinderDTO
{
    public bool CanIncludesPorn { get; set; } = false;
    public bool CanIncludesGore { get; set; } = false;
    public bool CanIncludesPolitics { get; set; } = false;
    public bool CanIncludesSlurs { get; set; } = false;
    public bool CanIncludesSensitiveContent { get; set; } = false;
    public bool? SealOfQuality { get; set; }
    public bool? MustHaveScore { get; set; }
    [Range(1, 5)]
    public int? ScoreLowerLimit { get; set; }
    [Range(1, 5)]
    public int? ScoreHigherLimit { get; set; }
    public DateTime? CreatedAtFrom { get; set; }
    public DateTime? CreatedAtTo { get; set; }
    public DateTime? VersionCreatedAtFrom { get; set; }
    public DateTime? VersionCreatedAtTo { get; set; }
    [StringLength(TextLimits.RESOURCE_NAME_SEARCH)]
    public string? NameLike { get; set; }
    [StringLength(TextLimits.SEARCH)]
    public string? AuthorLike { get; set; }
    [Required]
    public ResourceSortingDTO Sorting { get; set; } = new();
    public long[] TypeIds { get; set; } = [];
    public long[] CategoryIDs { get; set; } = [];
    public long[] VideogameIDs { get; set; } = [];
    [MaxItemLength(TextLimits.USER_ID)]
    public string[] AuthorsIDs { get; set; } = [];
    [StringLength(TextLimits.TAG)]
    public string? TagLike { get; set; }
}
public sealed class ResourceFinderViewDTO
{
    public required IdNamePair ResourceIdName { get; set; }
    public required UserNameDTO SubmitterUserSummary { get; set; }
    public required UserNameDTO[] AuthorsUserSummary { get; set; } = [];
    public required IdNamePair[] ExternalAuthors { get; set; } = [];
    public required FileResponse MainVersion { get; set; }
    public required IdNamePair[] Tags { get; set; }
    public required IdNamePair[] Types { get; set; }
    public required IdNamePair[] Categories { get; set; }
    public required IdNamePair Videogame { get; set; }
    public required bool IncludesPorn { get; set; }
    public required bool IncludesGore { get; set; }
    public required bool IncludesPolitics { get; set; }
    public required bool IncludesSlurs { get; set; }
    public required bool IncludesSensitiveContent { get; set; }
    public required bool SealOfQuality { get; set; }
    public required long Downloads { get; set; }
    public required long Votes { get; set; }
    public required double Score { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required DateTime VersionCreatedAt { get; set; }
    public required string Version { get; set; }
}
public sealed class ResourceReadDTO
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? RepositoryURL { get; set; }
    public bool AlreadyModerated { get; set; }
    public bool CanEdit { get; set; }

    public bool IncludesPorn { get; set; }
    public bool IncludesGore { get; set; }
    public bool IncludesPolitics { get; set; }
    public bool IncludesSlurs { get; set; }
    public bool IncludesSensitiveContent { get; set; }

    public required IdNamePair Videogame { get; set; }
    public required IdNamePair Visibility { get; set; }
    public List<ResourceTypeReadDTO> Types { get; set; } = [];
    public List<ResourceTagReadDTO> Tags { get; set; } = [];
}
public sealed record ResourceTypeReadDTO(long Id, long ResourceTypeId, string Name,
    long CategoryId, string CategoryName);
public sealed record ResourceTagReadDTO(long Id, long TagId, string Name);
