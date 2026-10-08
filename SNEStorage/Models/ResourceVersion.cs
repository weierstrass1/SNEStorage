using SNEStorage.DTOs;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SNEStorage.Models;
public partial class ResourceVersion
{
    public required long Id { get; set; }
    public string? ChangeLog { get; set; }
    public string? Version { get; set; }
    public int Downloads { get; set; }
    public required long ResourceId { get; set; }
    public required long FileId { get; set; }
    public required string SubmitterUserId { get; set; }
    public DateTime CreatedAt { get; set; }
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
