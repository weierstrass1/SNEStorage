using System.ComponentModel.DataAnnotations;

namespace SNEStorage.DTOs;

public class EditRole
{
    [Required]
    [StringLength(TextLimits.ROLE)]
    public required string RoleName { get; set; }
    [Required]
    [StringLength(256)]
    public required string Username { get; set; }
}
