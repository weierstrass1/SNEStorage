using System.ComponentModel.DataAnnotations;

namespace SNEStorage.DTOs;
public class EditProfileDTO
{
    public string? UsernameColor { get; set; }
    public IFormFile? File { get; set; }
    public string? Bio { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Patreon { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Paypal { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? BuyMeACoffee { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Discord { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Twitter { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Facebook { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Youtube { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Instagram { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Steam { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Twitch { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Github { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? NintendoNetwork { get; set; }
    public long? EmailVisibilityId { get; set; }
    public long? TimeZoneId { get; set; }
}
