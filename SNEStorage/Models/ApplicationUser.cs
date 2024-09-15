using Microsoft.AspNetCore.Identity;

namespace SNEStorage.Models;

public class ApplicationUser : IdentityUser
{
    public virtual ICollection<Comment> Comments { get; set; } = [];
    public virtual ICollection<LikeUsers> LikesUsers { get; set; } = [];
    public virtual ICollection<ScoreUsers> ScoresUsers { get; set; } = [];
    public virtual ICollection<Resource> Resources { get; set; } = [];
    public virtual ICollection<TeamUsers> TeamsUsers { get; set; } = [];
    public virtual ICollection<Flag> Flags { get; set; } = [];
    public virtual UserInfo? UserInfo { get; set; }
}
