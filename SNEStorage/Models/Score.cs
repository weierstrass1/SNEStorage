namespace SNEStorage.Models;
public partial class Score
{
    public long Id { get; set; }
    public virtual Resource? Resource { get; set; }
    public virtual Comment? Comment { get; set; }
    public virtual ICollection<LikeUsers> LikesUsers { get; set; } = [];
    public virtual ICollection<ScoreUsers> ScoresUsers { get; set; } = [];
}
