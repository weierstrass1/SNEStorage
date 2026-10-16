using System.ComponentModel.DataAnnotations;

namespace SNEStorage.DTOs;
public enum FlagType
{
    Resource,
    Comment
}
public class ResourceFlagCreateDTO
{
    [Required]
    public required long ReasonID { get; set; }
    [Required]
    public required long ResourceID { get; set; }
}
public class CommentFlagCreateDTO
{
    [Required]
    public required long ReasonID { get; set; }
    [Required]
    public required long CommentID { get; set; }
}
public class FlagFilterDTO
{
    [Range(1,5)]
    public int? LowestPriority { get; set; }
    [Range(1, 5)]
    public int? HighestPriority { get; set; }
    public bool? Resolved { get; set; }
    public long? ReasonID { get; set; }
    public DateTime? CreatedAtFrom { get; set; }
    public DateTime? CreatedAtTo { get; set; }
    public FlagType? Type { get; set; }
}
public enum FlagSort
{
    HighestPriority,
    LowestPriority,
    RecentFirst,
    RecentLast
}
public class FlagReadDTO
{
    public long Id { get; set; }
    public required UserNameDTO UserSummary { get; set; }
    public long ReasonId { get; set; }
    public required string ReasonDescription { get; set; }
    public bool Resolved { get; set; }
    public int Priority { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public IdNamePair? Resource { get; set; }
    public long? CommentId { get; set; }
}
