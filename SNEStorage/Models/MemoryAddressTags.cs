namespace SNEStorage.Models;

public partial class MemoryAddressTags
{
    public long Id { get; set; }
    public long MemoryAddressId { get; set; }
    public long TagId { get; set; }
    public virtual MemoryAddress? MemoryAddress { get; set; }
    public virtual Tag? Tag { get; set; }
}
