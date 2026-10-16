namespace SNEStorage.Models;

public partial class CommentFlag
{
    public required long Id { get; set; }
    public required long FlagId {  get; set; }
    public required long CommentId {  get; set; }
    public virtual Flag? Flag { get; set; }
    public virtual Comment? Comment { get; set; }
}
