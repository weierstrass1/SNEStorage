namespace SNEStorage.Models;
public partial class FileType
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required string Value { get; set; }
    public virtual ICollection<File> Files { get; set; } = [];
}
