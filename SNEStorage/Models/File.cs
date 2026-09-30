namespace SNEStorage.Models;
public partial class File
{
    public long Id { get; set; }
    public required string URL { get; set; }
    public long FileTypeId { get; set; }
    public virtual FileType? FileType { get; set; }
    public virtual ICollection<Resource> Resources { get; set; } = [];
    public virtual ICollection<ResourceMedia> PreviewMedia { get; set; } = [];
    public virtual UserInfo? UserInfo { get; set; }
}
