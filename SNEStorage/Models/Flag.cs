using SNEStorage.DTOs;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SNEStorage.Models;

public partial class Flag
{
    public long Id { get; set; }
    public required string UserId { get; set; }
    public long ReasonId { get; set; }
    public DateTime Date { get; set; }
    [JsonIgnore]
    public virtual ApplicationUser? User { get; set; }
    [NotMapped]
    public UserNameDTO? UserSummary { get; set; }
    public virtual Reason? Reason { get; set; }
    public virtual ResourceFlags? ResourceFlags { get; set; }
    public virtual MemoryAddressFlags? MemoryAddressFlags { get; set; }
}
