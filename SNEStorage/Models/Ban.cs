namespace SNEStorage.Models;

public partial class Ban
{
    public long Id { get; set; }
    public required string UserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpireDate { get; set; }
    public bool IsPermanent { get; set; }
    public required string Reasons {  get; set; }
    public virtual ApplicationUser? User { get; set; }
}
