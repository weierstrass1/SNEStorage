using Microsoft.AspNetCore.Identity;

namespace SNEStorage.Models;

public class ApplicationUser : IdentityUser
{
    public virtual ICollection<Comment> Comments { get; set; } = [];
    public virtual ICollection<LikeUsers> LikesUsers { get; set; } = [];
    public virtual ICollection<ScoreUsers> ScoresUsers { get; set; } = [];
    public virtual ICollection<Resource> Resources { get; set; } = [];
}
