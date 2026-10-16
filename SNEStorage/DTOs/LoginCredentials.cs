using System.ComponentModel.DataAnnotations;
namespace SNEStorage.DTOs;

public class LoginCredentials
{
    [Required]
    [Display(Name = "Username")]
    [StringLength(256)]
    public string? User { get; set; }
    [Required]
    [StringLength(TextLimits.LOGIN_PASSWORD)]
    public string? Password { get; set; }
}
