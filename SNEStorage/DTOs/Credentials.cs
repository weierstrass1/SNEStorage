using System.ComponentModel.DataAnnotations;

namespace SNEStorage.DTOs;

public class Credentials
{
    [Required]
    public string User { get; set; }
    [Required]
    [EmailAddress]
    public string Email { get; set; }
    [Required]
    public string Password { get; set; }
}
