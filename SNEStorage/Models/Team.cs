namespace SNEStorage.Models;

public partial class Team
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public virtual ICollection<TeamUsers> TeamsUsers { get; set; } = [];
    public virtual ICollection<ResourceTeams> ResourcesTeams { get; set; } = [];
}
