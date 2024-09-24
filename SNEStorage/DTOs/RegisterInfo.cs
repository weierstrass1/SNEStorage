using Microsoft.AspNetCore.Mvc;
using SNEStorage.Models;
using System.ComponentModel.DataAnnotations;

namespace SNEStorage.DTOs;

public class RegisterInfo
{
    [Required]
    [Display(Name = "Username")]
    public string User { get; set; }
    [Required]
    [EmailAddress]
    public string Email { get; set; }
    [Required]
    public string Password { get; set; }
    [Required]
    [Display(Name = "Repeat Password")]
    public string PasswordRepeat { get; set; }
    [Display(Name = "Username Color")]
    public string? UsernameColor { get; set; }
    public DateOnly Birthday { get; set; }
    [Display(Name = "Email Visibility")]
    public long EmailVisibilityId { get; set; }
    [Display(Name = "Time Zone")]
    public long TimeZoneId { get; set; }
    [BindProperty]
    public IFormFile Avatar { get; set; }
}
