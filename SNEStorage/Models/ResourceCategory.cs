using System.ComponentModel.DataAnnotations;
namespace SNEStorage.Models;

public partial class ResourceCategory
{
    public required long Id { get; set; }
    [Required(AllowEmptyStrings = false)]
    [StringLength(20)]
    public required string Name { get; set; }
    public virtual ICollection<ResourceType> Types { get; set; } = [];
}
