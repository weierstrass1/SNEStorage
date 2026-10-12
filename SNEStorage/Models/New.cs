namespace SNEStorage.Models;
public partial class New
{
    public required long Id { get; set; }
    public required string PosterUserId { get; set; }
    public required DateTime CreatedAt { get; set; }
    public required string Title { get; set; }
    public required string Text { get; set; }
    public virtual ApplicationUser? PosterUser { get; set; }
}
