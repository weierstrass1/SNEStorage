using System.ComponentModel.DataAnnotations;

namespace SNEStorage.DTOs;

public class EditRole
{
    [Required]
    public string RoleName { get; set; }
    [Required]
    public string Username { get; set; }
}
