using System.ComponentModel.DataAnnotations;
namespace SNEStorage.Models;

public partial class Videogame
{
    public required long Id { get; set; }
    [Required(AllowEmptyStrings = false)]
    public required string Name { get; set; }
    public required DateOnly? ReleaseDate { get; set; }
    public string? Publisher { get; set; }
    public virtual ICollection<Resource> Resources { get; set; } = [];
    public virtual ICollection<MemoryAddress> MemoryAddresses { get; set; } = [];
}
