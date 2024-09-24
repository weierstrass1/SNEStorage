using System.ComponentModel.DataAnnotations;
namespace SNEStorage.DTOs;
public class LoginCredentials
{
    [Required]
    [Display(Name = "Username")]
    public string? User { get; set; }
    [Required]
    public string? Password { get; set; }
}
