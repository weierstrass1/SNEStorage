namespace SNEStorage.Models;

public partial class ResourceAuthors
{
    public long Id { get; set; }
    public string? UserId { get; set; }
    public long ResourceId { get; set; }
    public virtual ApplicationUser? User { get; set; }
    public virtual Resource? Resource { get; set; }
}
