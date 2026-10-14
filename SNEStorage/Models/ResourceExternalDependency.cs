using System.ComponentModel.DataAnnotations;
namespace SNEStorage.Models;

public partial class ResourceExternalDependency
{
    public required long Id { get; set; }
    public required long ResourceId { get; set; }
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public required string DependencyName { get; set; }
    [Required(AllowEmptyStrings = false)]
    [StringLength(255)]
    public required string DependencyURL { get; set; }
    [StringLength(255)]
    public string? Mirror1 { get; set; }
    [StringLength(255)]
    public string? Mirror2 { get; set; }
    [StringLength(255)]
    public string? Mirror3 { get; set; }
    [StringLength(255)]
    public string? Mirror4 { get; set; }
    public virtual Resource? Resource { get; set; }
}
