namespace SNEStorage.Models;

public partial class ResourceType
{
    public required long Id { get; set; }
    public string? Name { get; set; }
    public required long CategoryId { get; set; }
    public virtual ResourceCategory? Category { get; set; }
    public virtual ICollection<ResourceResourceTypes> ResourceResourceTypes { get; set; } = [];
}
