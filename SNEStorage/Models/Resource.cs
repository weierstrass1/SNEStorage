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
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdate { get; set; }
    public bool IncludesPorn { get; set; }
    public bool IncludesGore { get; set; }
    public bool IncludesPolitics { get; set; }
    public bool IncludesSlurs { get; set; }
    public bool IncludesSensitiveContent { get; set; }
    public long FileId { get; set; }
    public required string SubmitterUserId { get; set; }
    public long ScoreId { get; set; }
    public long VisibilityId { get; set; }
    public virtual ResourceType? ResourceType { get; set; }
    public virtual Videogame? Videogame { get; set; }
    public virtual File? File { get; set; }
    public virtual ApplicationUser? SubmitterUser { get; set; }
    public virtual Score? Score { get; set; }
    public virtual Visibility? Visibility { get; set; }
    public virtual ICollection<ResourceComments> ResourceComments { get; set; } = [];
    public virtual ICollection<ResourceFlags> ResourceFlags { get; set; } = [];
    public virtual ICollection<ResourceAuthors> ResourcesAuthors { get; set; } = [];
    public virtual ICollection<ResourceTeams> ResourcesTeams { get; set; } = [];
    public virtual ICollection<ResourceExternalAuthor> ResourceExternalAuthors { get; set; } = [];
    public virtual ICollection<ResourceTags> ResourceTags { get; set; } = [];
}
