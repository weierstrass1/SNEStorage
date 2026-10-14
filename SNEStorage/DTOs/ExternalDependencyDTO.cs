using System.ComponentModel.DataAnnotations;
namespace SNEStorage.DTOs;

public class ExternalDependencyDTO
{
    [Required(AllowEmptyStrings = true)]
    [StringLength(100)]
    public required string DependencyName { get; set; }
    [Required(AllowEmptyStrings = true)]
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
}
