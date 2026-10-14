using System.ComponentModel.DataAnnotations;
using SNEStorage.DTOs;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SNEStorage.Models;

public partial class Resource
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
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool IncludesPorn { get; set; } = false;
    public bool IncludesGore { get; set; } = false;
    public bool IncludesPolitics { get; set; } = false;
    public bool IncludesSlurs { get; set; } = false;
    public bool IncludesSensitiveContent { get; set; } = false;
    public required long ScoreId { get; set; }
    public required long VisibilityId { get; set; }
    [StringLength(2048)]
    [RegularExpression(@"(?i)\Ahttps?://[^\s<>""\\]+\z")]
    public string? RepositoryURL { get; set; }
    public bool AlreadyModerated { get; set; } = false;
    [Required(AllowEmptyStrings = true)]
    [StringLength(450)]
    public required string SubmitterId { get; set; }
    [JsonIgnore]
    public virtual ApplicationUser? Submitter { get; set; }
    [NotMapped]
    public UserNameDTO? UserSummary { get; set; }
    public virtual Videogame? Videogame { get; set; }
    public virtual Score? Score { get; set; }
    public virtual Visibility? Visibility { get; set; }
    public virtual ICollection<ResourcePreview> ResourcePreviews { get; set; } = [];
    public virtual ICollection<ResourceDependency> ResourceDependencies { get; set; } = [];
    public virtual ICollection<ResourceDependency> Dependents { get; set; } = [];
    public virtual ICollection<ResourceExternalDependency> ResourceExternalDependencies { get; set; } = [];
    public virtual ICollection<ResourceVersion> ResourceVersions { get; set; } = [];
    public virtual ICollection<ResourceResourceTypes> ResourceResourceTypes { get; set; } = [];
    public virtual ICollection<ResourceComments> ResourceComments { get; set; } = [];
    public virtual ICollection<ResourceFlags> ResourceFlags { get; set; } = [];
    public virtual ICollection<ResourceAuthors> ResourcesAuthors { get; set; } = [];
    public virtual ICollection<ResourceTeams> ResourcesTeams { get; set; } = [];
    public virtual ICollection<ResourceExternalAuthor> ResourceExternalAuthors { get; set; } = [];
    public virtual ICollection<ResourceTags> ResourceTags { get; set; } = [];
}
