using System.ComponentModel.DataAnnotations;
using SNEStorage.DTOs;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SNEStorage.Models;

public partial class ResourceAuthors
{
    public long Id { get; set; }
    [Required(AllowEmptyStrings = true)]
    [StringLength(450)]
    public required string UserId { get; set; }
    public required long ResourceId { get; set; }
    [JsonIgnore]
    public virtual ApplicationUser? User { get; set; }
    [NotMapped]
    public UserNameDTO? UserSummary { get; set; }
    public virtual Resource? Resource { get; set; }
}
