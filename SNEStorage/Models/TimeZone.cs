using System.ComponentModel.DataAnnotations;
namespace SNEStorage.Models;

public partial class TimeZone
{
    public required long Id { get; set; }
    [Required(AllowEmptyStrings = false)]
    [StringLength(10)]
    public required string Name { get; set; }
    public required int Value { get; set; }
    public virtual ICollection<UserInfo> UserInfos { get; set; } = [];
}
