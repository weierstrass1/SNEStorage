using System.ComponentModel.DataAnnotations;

namespace SNEStorage.DTOs;

public class ChangePasswordDTO
{
    [Required]
    [StringLength(450)]
    public required string UserId { get; set; }

    [Required]
    [StringLength(TextLimits.TOKEN)]
    public required string Token { get; set; }

    [Required]
    [StringLength(TextLimits.PASSWORD)]
    public required string NewPassword { get; set; }

    [Required]
    [Compare(nameof(NewPassword))]
    [StringLength(TextLimits.PASSWORD)]
    public required string ConfirmPassword { get; set; }
}
