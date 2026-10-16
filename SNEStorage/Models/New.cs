using System.ComponentModel.DataAnnotations;
namespace SNEStorage.Models;

public partial class New
{
    public required long Id { get; set; }
    [Required(AllowEmptyStrings = true)]
    [StringLength(450)]
    public required string PosterUserId { get; set; }
    public required DateTime CreatedAt { get; set; }
    [Required]
    [StringLength(200)]
    public required string Title { get; set; }
    [Required]
    public required string Text { get; set; }
    public long ScoreId { get; set; }
    public virtual Score? Score { get; set; }
    public virtual ApplicationUser? PosterUser { get; set; }
}
