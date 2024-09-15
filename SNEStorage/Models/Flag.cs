namespace SNEStorage.Models;

public partial class Flag
{
    public long Id { get; set; }
    public required string UserId { get; set; }
    public long ReasonId { get; set; }
    public DateTime Date { get; set; }
    public virtual ApplicationUser? User { get; set; }
    public virtual Reason? Reason { get; set; }
    public virtual ResourceFlags? ResourceFlags { get; set; }
    public virtual MemoryAddressFlags? MemoryAddressFlags { get; set; }
}
