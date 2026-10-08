namespace SNEStorage.Models;

public partial class ResourceResourceTypes
{
    public required long Id { get; set; }
    public required long ResourceId { get; set; }
    public required long ResourceTypeId { get; set; }
    public virtual Resource? Resource { get; set; }
    public virtual ResourceType? ResourceType { get; set; }
}
