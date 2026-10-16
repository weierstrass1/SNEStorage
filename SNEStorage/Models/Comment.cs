using System.ComponentModel.DataAnnotations;
using SNEStorage.DTOs;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SNEStorage.Models;

public partial class Comment
{
    public long Id { get; set; }
    public DateTime PublishDate { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public bool Deleted { get; set; } = false;
    [Required(AllowEmptyStrings = true)]
    [StringLength(5000)]
    public required string Text { get; set; }
    [Required(AllowEmptyStrings = true)]
    [StringLength(450)]
    public required string UserId { get; set; }
    public long ScoreId { get; set; }
    [JsonIgnore]
    public virtual ApplicationUser? User { get; set; }
    [NotMapped]
    public UserNameDTO? UserSummary { get; set; }
    public virtual Score? Score { get; set; }
    public virtual ResourceComments? ResourceComment { get; set; }
    public virtual ICollection<CommentComment> Childrens { get; set; } = [];
    public virtual CommentComment? Parent { get; set; }
    public virtual ICollection<CommentFlag> CommentFlags { get; set; } = [];
}
