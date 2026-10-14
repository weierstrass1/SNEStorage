using System.ComponentModel.DataAnnotations;

namespace SNEStorage.DTOs;

public class EditProfileDTO
{
    [StringLength(7)]
    [RegularExpression(@"\A#[0-9A-Fa-f]{6}\z")]
    public string? UsernameColor { get; set; }
    public IFormFile? File { get; set; }
    [StringLength(2000)]
    public string? Bio { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(100)]
    public string? Patreon { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(100)]
    public string? Paypal { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(100)]
    public string? BuyMeACoffee { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(100)]
    public string? Discord { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(100)]
    public string? Twitter { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(100)]
    public string? Facebook { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(100)]
    public string? Youtube { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(100)]
    public string? Instagram { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(100)]
    public string? Steam { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(100)]
    public string? Twitch { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(100)]
    public string? Github { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(100)]
    public string? NintendoNetwork { get; set; }
    public long? EmailVisibilityId { get; set; }
    public long? TimeZoneId { get; set; }
}
