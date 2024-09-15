namespace SNEStorage.Models;

public partial class TeamUsers
{
    public long Id { get; set; }
    public required string UserId { get; set; }
    public required long TeamId { get; set; }
    public virtual ApplicationUser? User { get; set; }
    public virtual Team? Team { get; set; }
}
