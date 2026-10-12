using System.ComponentModel.DataAnnotations;

namespace SNEStorage.DTOs;
public class ChangePasswordDTO
{
    [Required]
    public required string UserId { get; set; }

    [Required]
    public required string Token { get; set; }

    [Required]
    public required string NewPassword { get; set; }

    [Required]
    [Compare(nameof(NewPassword))]
    public required string ConfirmPassword { get; set; }
}
