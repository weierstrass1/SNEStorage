using System.ComponentModel.DataAnnotations;
using SNEStorage.DTOs;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SNEStorage.Models;

public partial class ScoreUsers
{
    public required long Id { get; set; }
    public required long ScoreId { get; set; }
    [Required(AllowEmptyStrings = true)]
    [StringLength(450)]
    public required string UserId { get; set; }
    [Range(typeof(long), "1", "5")]
    public required long Value { get; set; }
    [JsonIgnore]
    public virtual ApplicationUser? User { get; set; }
    public virtual Score? Score { get; set; }
    [NotMapped]
    public UserNameDTO? UserSummary { get; set; }
}
