using Microsoft.AspNetCore.Identity;

namespace SNEStorage.Models;
public partial class Comment
{
    public long Id { get; set; }
    public DateTime PublishDate { get; set; }
    public required string Text { get; set; }
    public long UserId { get; set; }
    public long ScoreId { get; set; }
    public virtual ApplicationUser? User { get; set; }
    public virtual Score? Score { get; set; }
    public virtual ICollection<ResourceComments> ResourceComments { get; set; } = [];
}
