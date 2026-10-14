using System.ComponentModel.DataAnnotations;
namespace SNEStorage.Models;

public partial class FileType
{
    public long Id { get; set; }
    [Required(AllowEmptyStrings = false)]
    [StringLength(10)]
    public required string Name { get; set; }
    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public required string Value { get; set; }
    public virtual ICollection<File> Files { get; set; } = [];
}
