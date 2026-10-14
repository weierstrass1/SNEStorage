using System.ComponentModel.DataAnnotations;

namespace SNEStorage.DTOs;

public class EditRole
{
    [Required]
    public required string RoleName { get; set; }
    [Required]
    [StringLength(256)]
    public required string Username { get; set; }
}
