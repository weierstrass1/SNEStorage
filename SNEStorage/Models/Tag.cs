namespace SNEStorage.Models;

public partial class Tag
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public virtual ICollection<ResourceTags> ResourceTags { get; set; } = [];
    public virtual ICollection<MemoryAddressTags> MemoryAddressTags { get; set; } = [];
}
