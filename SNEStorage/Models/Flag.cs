using System.ComponentModel.DataAnnotations;
using SNEStorage.DTOs;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SNEStorage.Models;

public partial class Flag
{
    public long Id { get; set; }
    [Required(AllowEmptyStrings = true)]
    [StringLength(450)]
    public required string UserId { get; set; }
    public long ReasonId { get; set; }
    public bool Resolved { get; set; } = false;
    public required DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    [JsonIgnore]
    public virtual ApplicationUser? User { get; set; }
    [NotMapped]
    public UserNameDTO? UserSummary { get; set; }
    public virtual Reason? Reason { get; set; }
    public virtual ResourceFlags? ResourceFlags { get; set; }
    public virtual CommentFlag? CommentFlags { get; set; }
    public virtual MemoryAddressFlags? MemoryAddressFlags { get; set; }
}
