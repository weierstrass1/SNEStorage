using SNEStorage.DTOs;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SNEStorage.Models;

public partial class Ban
{
    public long Id { get; set; }
    public required string UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpireDate { get; set; }
    public bool IsPermanent { get; set; }
    public required string Reasons {  get; set; }
    [JsonIgnore]
    public virtual ApplicationUser? User { get; set; }
    [NotMapped]
    public UserNameDTO? UserSummary { get; set; }
}
