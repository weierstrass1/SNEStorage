using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace SNEStorage.DTOs;

public class RegisterInfo
{
    [Required]
    [Display(Name = "Username")]
    [StringLength(TextLimits.USERNAME)]
    public required string User { get; set; }
    [Required]
    [EmailAddress]
    [StringLength(TextLimits.EMAIL)]
    public required string Email { get; set; }
    [Required]
    [StringLength(TextLimits.PASSWORD)]
    public required string Password { get; set; }
    [Required]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Repeat Password")]
    [StringLength(TextLimits.PASSWORD)]
    public required string PasswordRepeat { get; set; }
    [Required]
    [StringLength(7)]
    [RegularExpression(@"\A#[0-9A-Fa-f]{6}\z")]
    [Display(Name = "Username Color")]
    public required string UsernameColor { get; set; }
    public required DateOnly Birthday { get; set; }
    [Display(Name = "Email Visibility")]
    public required long EmailVisibilityId { get; set; }
    [Display(Name = "Time Zone")]
    public required long TimeZoneId { get; set; }
    [Required]
    [Display(Name = "Avatar")]
    [BindProperty]
    public required IFormFile Avatar { get; set; }
}
