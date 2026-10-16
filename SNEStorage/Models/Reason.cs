using System.ComponentModel.DataAnnotations;
namespace SNEStorage.Models;

public partial class Reason
{
    public long Id { get; set; }
    [Range(1, 5)]
    public required int Priority { get; set; }
    [Required(AllowEmptyStrings = false)]
    public required string Description { get; set; }
    public required long ReasonCategoryId { get; set; }
    public virtual ReasonCategory? ReasonCategory { get; set; }
    public virtual ICollection<Flag> Flags { get; set; } = [];
}
