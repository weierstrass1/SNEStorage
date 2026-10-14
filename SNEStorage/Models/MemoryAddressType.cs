using System.ComponentModel.DataAnnotations;
namespace SNEStorage.Models;

public partial class MemoryAddressType
{
    public long Id { get; set; }
    [Required(AllowEmptyStrings = false)]
    [StringLength(50)]
    public required string Name { get; set; }
    public virtual ICollection<MemoryAddress> MemoryAddresses { get; set; } = [];
}
