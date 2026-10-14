using System.ComponentModel.DataAnnotations;
using SNEStorage.DTOs;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SNEStorage.Models;

public partial class ResourceVersion
{
    public required long Id { get; set; }
    [StringLength(10000, MinimumLength = 10)]
    public required string ChangeLog { get; set; }
    [StringLength(25)]
    [RegularExpression(@"(?i)\A[0-9]{1,4}(?:\.[0-9]{1,4}){0,2}(?:-(?:alpha|beta|rc)(?:\.[0-9]{1,4})?)?\z")]
    public required string Version { get; set; }
    public int Downloads { get; set; }
    public required long ResourceId { get; set; }
    public required long FileId { get; set; }
    [Required(AllowEmptyStrings = true)]
    public required string SubmitterUserId { get; set; }
    public required DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool AlreadyModerated { get; set; } = false;
    public bool SealOfQualityRequested { get; set; } = false;
    public bool SealOfQuality { get; set; } = false;
    public virtual Resource? Resource { get; set; }
    public virtual File? File { get; set; }
    [JsonIgnore]
    public virtual ApplicationUser? SubmitterUser { get; set; }
    [NotMapped]
    public UserNameDTO? UserSummary { get; set; }
}
