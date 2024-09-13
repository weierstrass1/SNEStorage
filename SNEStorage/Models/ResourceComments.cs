namespace SNEStorage.Models;
public partial class ResourceComments
{
    public long Id { get; set; }
    public long CommentId { get; set; }
    public long ResourceId { get; set; }
    public virtual Comment? Comment { get; set; }
    public virtual Resource? Resource { get; set; }
}
