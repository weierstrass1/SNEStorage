using System.ComponentModel.DataAnnotations;
namespace SNEStorage.Models;

public partial class Visibility
{
    public required long Id { get; set; }
    [Required(AllowEmptyStrings = false)]
    [StringLength(100)]
    public required string Name { get; set; }
    public required bool ResourceOnly { get; set; }
    public virtual ICollection<UserInfo> UserInfos { get; set; } = [];
    public virtual ICollection<Resource> Resources { get; set; } = [];
}
