namespace SNEStorage.Models;
public partial class CommentComment
{
    public required long Id { get; set; }
    public required long ParentCommentId { get; set; }
    public required long ChildCommentId { get; set; }
    public virtual Comment? ParentComment { get; set; }
    public virtual Comment? ChildComment { get; set; }
}
