using System.ComponentModel.DataAnnotations;
namespace SNEStorage.Models;

public partial class Reason
{
    public long Id { get; set; }
    [Required(AllowEmptyStrings = false)]
    public required string Description { get; set; }
    public virtual ICollection<Flag> Flags { get; set; } = [];
}
