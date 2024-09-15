namespace SNEStorage.Models;

public partial class MemoryAddressValues
{
    public long Id { get; set; }
    public long MemoryAddressId { get; set; }
    public string Value { get; set; }
    public string Description { get; set; }
    public virtual MemoryAddress? MemoryAddress { get; set; }
}
