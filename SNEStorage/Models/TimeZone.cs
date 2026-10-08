namespace SNEStorage.Models;

public partial class TimeZone
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public required int Value { get; set; }
    public virtual ICollection<UserInfo> UserInfos { get; set; } = [];
}
