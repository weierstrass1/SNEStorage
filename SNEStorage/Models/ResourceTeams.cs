namespace SNEStorage.Models;

public partial class ResourceTeams
{
    public long Id { get; set; }
    public long TeamId { get; set; }
    public long ResourceId { get; set; }
    public virtual Team? Team { get; set; }
    public virtual Resource? Resource { get; set; }
}
