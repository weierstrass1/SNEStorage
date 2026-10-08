using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace SNEStorage.DTOs;

public class RegisterInfo
{
    [Required]
    [Display(Name = "Username")]
    public required string User { get; set; }
    [Required]
    [EmailAddress]
    public required string Email { get; set; }
    [Required]
    public required string Password { get; set; }
    [Required]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Repeat Password")]
    public required string PasswordRepeat { get; set; }
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
