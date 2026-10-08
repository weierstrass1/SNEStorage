namespace SNEStorage.Models;
public partial class ResourceDependency
{
    public required long Id { get; set; }
    public required long ResourceId { get; set; }
    public required long DependencyResourceId { get; set; }
    public virtual Resource? Resource { get; set; }
    public virtual Resource? DependencyResource { get; set; }
}
