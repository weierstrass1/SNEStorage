namespace SNEStorage.Models;
public partial class ReasonCategory
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public virtual ICollection<Reason> Reasons { get; set; } = [];
}
