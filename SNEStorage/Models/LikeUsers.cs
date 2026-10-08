using SNEStorage.DTOs;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SNEStorage.Models;
public partial class LikeUsers
{
    public long Id { get; set; }
    public long ScoreId { get; set; }
    public required string UserId { get; set; }
    public bool Value { get; set; }
    [JsonIgnore]
    public virtual ApplicationUser? User { get; set; }
    [NotMapped]
    public UserNameDTO? UserSummary { get; set; }
    public virtual Score? Score { get; set; }
}
