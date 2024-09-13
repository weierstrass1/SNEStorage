using Microsoft.AspNetCore.Identity;

namespace SNEStorage.Models;

public partial class Resource
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public long? VideogameId { get; set; }
    public long? ResourceTypeId { get; set; }
    public string? Usage { get; set; }
    public string? Description { get; set; }
    public string? Version { get; set; }
    public int? Downloads { get; set; }
    public long FileId { get; set; }
    public long SubmitterUserId { get; set; }
    public long ScoreId { get; set; }
    public virtual ResourceType? ResourceType { get; set; }
    public virtual Videogame? Videogame { get; set; }
    public virtual File? File { get; set; }
    public virtual ApplicationUser? SubmitterUser { get; set; }
    public virtual Score? Score { get; set; }
    public virtual ICollection<ResourceComments> ResourceComments { get; set; } = [];
}
