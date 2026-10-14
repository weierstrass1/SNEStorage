namespace SNEStorage.Models;
public partial class ResourceCategory
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public virtual ICollection<ResourceType> Types { get; set; } = [];
}
