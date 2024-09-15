namespace SNEStorage.Models
{
    public partial class TimeZone
    {
        public long Id { get; set; }
        public string Name { get; set; }
        public int Value { get; set; }
        public virtual ICollection<UserInfo> UserInfos { get; set; } = [];
    }
}
