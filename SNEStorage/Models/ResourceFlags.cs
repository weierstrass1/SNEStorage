namespace SNEStorage.Models;

public partial class ResourceFlags
{
    public required long Id { get; set; }
    public required long FlagId {  get; set; }
    public required long ResourceId {  get; set; }
    public virtual Flag? Flag { get; set; }
    public virtual Resource? Resource { get; set; }
}
