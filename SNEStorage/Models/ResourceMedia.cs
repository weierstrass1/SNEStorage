namespace SNEStorage.Models;

public partial class ResourceMedia
{
    public long Id { get; set; }
    public long ResourceId { get; set; }
    public long FileId { get; set; }
    public long? PosterFileId { get; set; }
    public string? Caption { get; set; }
    public int SortOrder { get; set; }
    public virtual Resource? Resource { get; set; }
    public virtual File? File { get; set; }
    public virtual File? PosterFile { get; set; }
}
