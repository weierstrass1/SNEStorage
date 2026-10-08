namespace SNEStorage.Models;
public partial class ResourceExternalDependency
{
    public required long Id { get; set; }
    public required long ResourceId { get; set; }
    public required string DependencyName { get; set; }
    public required string DependencyURL { get; set; }
    public string? Mirror1 { get; set; }
    public string? Mirror2 { get; set; }
    public string? Mirror3 { get; set; }
    public string? Mirror4 { get; set; }
    public virtual Resource? Resource { get; set; }
}
