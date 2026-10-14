using System.ComponentModel.DataAnnotations;
namespace SNEStorage.Models;

public partial class Team
{
    public required long Id { get; set; }
    [Required(AllowEmptyStrings = false)]
    public required string Name { get; set; }
    public required DateTime CreatedAt { get; set; }
    public virtual ICollection<TeamUsers> TeamsUsers { get; set; } = [];
    public virtual ICollection<ResourceTeams> ResourcesTeams { get; set; } = [];
}
