namespace SNEStorage.Models;
public partial class Score
{
    public int Id { get; set; }
    public virtual ICollection<Resource> Resources { get; set; } = [];
    public virtual ICollection<Comment> Comments { get; set; } = [];
    public virtual ICollection<LikeUsers> LikesUsers { get; set; } = [];
    public virtual ICollection<ScoreUsers> ScoresUsers { get; set; } = [];
}
