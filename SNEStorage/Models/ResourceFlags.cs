namespace SNEStorage.Models;

public partial class ResourceFlags
{
    public long Id { get; set; }
    public long FlagId {  get; set; }
    public long ResourceId {  get; set; }
    public virtual Flag? Flag { get; set; }
    public virtual Resource? Resource { get; set; }
}
