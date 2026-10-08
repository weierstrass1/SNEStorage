namespace SNEStorage.Models;
public partial class ResourcePreview
{
    public required long Id { get; set; }
    public required long ResourceId { get; set; }
    public required long FileId { get; set; }
    public virtual Resource? Resource { get; set; }
    public virtual File? File { get; set; }
}
