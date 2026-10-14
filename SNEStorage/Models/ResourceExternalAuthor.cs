using System.ComponentModel.DataAnnotations;
namespace SNEStorage.Models;

public partial class ResourceExternalAuthor
{
    public long Id { get; set; }
    [Required(AllowEmptyStrings = false)]
    [StringLength(200)]
    public required string Author { get; set; }
    public long ResourceId { get; set; }
    public virtual Resource? Resource { get; set; }
}
