namespace SNEStorage.Models;

public partial class MemoryAddressType
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public virtual ICollection<MemoryAddress> MemoryAddresses { get; set; } = [];
}
