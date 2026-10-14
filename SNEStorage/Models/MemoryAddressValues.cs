using System.ComponentModel.DataAnnotations;
namespace SNEStorage.Models;

public partial class MemoryAddressValues
{
    public long Id { get; set; }
    public long MemoryAddressId { get; set; }
    [Required(AllowEmptyStrings = false)]
    public required string Value { get; set; }
    [Required(AllowEmptyStrings = false)]
    public required string Description { get; set; }
    public virtual MemoryAddress? MemoryAddress { get; set; }
}
