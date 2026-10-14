using System.ComponentModel.DataAnnotations;
namespace SNEStorage.Models;

public partial class Tag
{
    public required long Id { get; set; }
    [Required(AllowEmptyStrings = false)]
    public required string Name { get; set; }
    public virtual ICollection<ResourceTags> ResourceTags { get; set; } = [];
    public virtual ICollection<MemoryAddressTags> MemoryAddressTags { get; set; } = [];
}
