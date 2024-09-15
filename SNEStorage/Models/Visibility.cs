namespace SNEStorage.Models
{
    public partial class Visibility
    {
        public long Id { get; set; }
        public string? Name { get; set; }
        public virtual ICollection<UserInfo> UserInfos { get; set; } = [];
        public virtual ICollection<Resource> Resources { get; set; } = [];
    }
}
