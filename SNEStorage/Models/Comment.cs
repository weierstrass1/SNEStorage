using Microsoft.AspNetCore.Identity;

namespace SNEStorage.Models;
public partial class Comment
{
    public long Id { get; set; }
    public DateTime PublishDate { get; set; }
    public required string Text { get; set; }
    public required string UserId { get; set; }
    public long ScoreId { get; set; }
    public long? ParentCommentId { get; set; }
    public virtual ApplicationUser? User { get; set; }
    public virtual Score? Score { get; set; }
    public virtual Comment? ParentComment { get; set; }
    public virtual ICollection<Comment> Replies { get; set; } = [];
    public virtual ICollection<ResourceComments> ResourceComments { get; set; } = [];
}
