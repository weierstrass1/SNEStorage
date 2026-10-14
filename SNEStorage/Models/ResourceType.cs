using System.ComponentModel.DataAnnotations;

namespace SNEStorage.Models;

public partial class ResourceType
{
    public required long Id { get; set; }
    [Required]
    public required string Name { get; set; }
    public required long CategoryId { get; set; }
    public virtual ResourceCategory? Category { get; set; }
    public virtual ICollection<ResourceResourceTypes> ResourceResourceTypes { get; set; } = [];
}
