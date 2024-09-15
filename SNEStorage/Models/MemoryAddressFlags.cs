namespace SNEStorage.Models;

public partial class MemoryAddressFlags
{
    public long Id { get; set; }
    public long FlagId {  get; set; }
    public long MemoryAddressId {  get; set; }
    public virtual Flag? Flag { get; set; }
    public virtual MemoryAddress? MemoryAddress { get; set; }
}
