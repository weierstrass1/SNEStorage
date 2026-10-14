using System.ComponentModel.DataAnnotations;
using SNEStorage.DTOs;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SNEStorage.Models;

public partial class TeamUsers
{
    public long Id { get; set; }
    [Required(AllowEmptyStrings = true)]
    [StringLength(450)]
    public required string UserId { get; set; }
    public required long TeamId { get; set; }
    [JsonIgnore]
    public virtual ApplicationUser? User { get; set; }
    public virtual Team? Team { get; set; }
    [NotMapped]
    public UserNameDTO? UserSummary { get; set; }
}
