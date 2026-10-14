using System.ComponentModel.DataAnnotations;
namespace SNEStorage.Models;

public partial class MemoryAddress
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime LastUpdate { get; set; }
    public long Size { get; set; }
    [Required(AllowEmptyStrings = true)]
    [StringLength(15)]
    public required string Address { get; set; }
    [Required(AllowEmptyStrings = true)]
    [StringLength(50)]
    public required string Name { get; set; }
    [Required(AllowEmptyStrings = true)]
    public required string Description { get; set; }
    public long TypeId { get; set; }
    public long VideoGameId { get; set; }
    public virtual MemoryAddressType? Type { get; set; }
    public virtual Videogame? Videogame { get; set; }
    public virtual ICollection<MemoryAddressValues> MemoryAddressValues { get; set; } = [];
    public virtual ICollection<MemoryAddressTags> MemoryAddressTags { get; set; } = [];
    public virtual ICollection<MemoryAddressFlags> MemoryAddressFlags { get; set; } = [];
}
