using System.ComponentModel.DataAnnotations;

namespace SNEStorage.DTOs;

public class ResourceEditDTO
{
    public long Id { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(100, MinimumLength = 2)]
    [RegularExpression(@"\A[\p{L}\p{Nd}][\p{L}\p{Nd} ]{0,98}[\p{L}\p{Nd}]\z")]
    public string? Name { get; set; }
    public long? VideogameId { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(10000, MinimumLength = 10)]
    public string? Description { get; set; }
    public bool? IncludesPorn { get; set; } = null;
    public bool? IncludesGore { get; set; } = null;
    public bool? IncludesPolitics { get; set; } = null;
    public bool? IncludesSlurs { get; set; } = null;
    public bool? IncludesSensitiveContent { get; set; } = null;
    public long? VisibilityId { get; set; }
    [Required]
    [MaxLength(50)]
    [MaxItemLength(TextLimits.TAG)]
    public string[] Tags { get; set; } = [];
    [Required]
    public long[] Types { get; set; } = [];
    [Required]
    public long[] RemovePreviewIDs { get; set; } = [];
    [Required]
    public long[] RemoveAuthorsIDs { get; set; } = [];
    [Required]
    public long[] RemoveExternalAuthorsIDs { get; set; } = [];
    [Required]
    public long[] RemoveDependencyIDs { get; set; } = [];
    [Required]
    public long[] RemoveExternalDependencyIDs { get; set; } = [];
    [Required]
    public long[] RemoveTagIDs { get; set; } = [];
    [Required]
    public long[] RemovedTypeIDs { get; set; } = [];
    [Required]
    [MaxItemLength(TextLimits.EXTERNAL_AUTHOR)]
    public string[] ExternalAuthors { get; set; } = [];
    [Required]
    [MaxItemLength(TextLimits.USER_ID)]
    public string[] AuthorsIDs { get; set; } = [];
    [Required]
    public long[] DependencyIDs { get; set; } = [];
    public bool? IAmAuthor { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(2048)]
    [RegularExpression(@"(?i)\Ahttps?://[^\s<>""\\]+\z")]
    public string? RepositoryURL { get; set; }
    [Required]
    public ExternalDependencyDTO[] ExternalDependencies { get; set; } = [];
    [Required]
    public IFormFile[] Previews { get; set; } = [];
}
