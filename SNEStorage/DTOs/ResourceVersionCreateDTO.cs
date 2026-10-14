using System.ComponentModel.DataAnnotations;
namespace SNEStorage.DTOs;

public class ResourceVersionCreateDTO
{
    [Required]
    [StringLength(25)]
    [RegularExpression(@"(?i)\A[0-9]{1,4}(?:\.[0-9]{1,4}){0,2}(?:-(?:alpha|beta|rc)(?:\.[0-9]{1,4})?)?\z")]
    public required string Version { get; set; }
    [Required]
    [StringLength(10000, MinimumLength = 10)]
    public required string ReleaseNotes { get; set; }
    public bool SealOfQualityRequested { get; set; } = false;
    [Required]
    public required IFormFile File { get; set; }
}
