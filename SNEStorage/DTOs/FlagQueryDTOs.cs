using System.ComponentModel.DataAnnotations;

namespace SNEStorage.DTOs;
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
    public int? LowestPriority;
    [Range(1, 5)]
    public int? HighestPriority;
    public bool? Resolved;
    public long? ReasonID;
    public DateTime? CreatedAtFrom;
    public DateTime? CreatedAtTo;
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
}
