using System.ComponentModel.DataAnnotations;
namespace SNEStorage.DTOs;

public class ResourceCreateDTO
{
    public required long Id { get; set; }
    [Required]
    [StringLength(100, MinimumLength = 2)]
    [RegularExpression(@"\A[\p{L}\p{Nd}][\p{L}\p{Nd} ]{0,98}[\p{L}\p{Nd}]\z")]
    public required string Name { get; set; }
    public required long VideogameId { get; set; }
    [Required]
    [StringLength(10000, MinimumLength = 10)]
    public required string Description { get; set; }
    public bool IncludesPorn { get; set; } = false;
    public bool IncludesGore { get; set; } = false;
    public bool IncludesPolitics { get; set; } = false;
    public bool IncludesSlurs { get; set; } = false;
    public bool IncludesSensitiveContent { get; set; } = false;
    public long VisibilityId { get; set; }
    [Required]
    [StringLength(10000, MinimumLength = 10)]
    public required string ChangeLog { get; set; }
    [Required]
    [StringLength(25)]
    [RegularExpression(@"(?i)\A[0-9]{1,4}(?:\.[0-9]{1,4}){0,2}(?:-(?:alpha|beta|rc)(?:\.[0-9]{1,4})?)?\z")]
    public required string Version { get; set; }
    [Required]
    public required IFormFile File { get; set; }
    [Required]
    [MaxLength(50)]
    public string[] Tags { get; set; } = [];
    [Required]
    [MinLength(1)]
    public long[] Types { get; set; } = [];
    [Required]
    [MaxLength(50)]
    public string[] ExternalAuthors { get; set; } = [];
    [Required]
    [MaxLength(50)]
    public string[] AuthorsIDs { get; set; } = [];
    [Required]
    [MaxLength(50)]
    public long[] DependencyIDs { get; set; } = [];
    [StringLength(2048)]
    [RegularExpression(@"(?i)\Ahttps?://[^\s<>""\\]+\z")]
    public string? RepositoryURL { get; set; }
    [Required]
    [MaxLength(50)]
    public ExternalDependencyDTO[] ExternalDependencies { get; set; } = [];
    [Required]
    [MaxLength(20)]
    public IFormFile[] Previews { get; set; } = [];
    public bool IAmAuthor { get; set; } = true;
    public bool SealOfQualityRequested { get; set; } = false;
}
