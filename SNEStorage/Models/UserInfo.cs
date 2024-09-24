namespace SNEStorage.Models;

public partial class UserInfo
{
    public long Id {  get; set; }
    public string? UsernameColor { get; set; }
    public string? Title {  get; set; }
    public long AvatarId { get; set; }
    public DateOnly Birthday { get; set; }
    public string? Bio {  get; set; }
    public string? Patreon { get; set; }
    public string? Paypal { get; set; }
    public string? BuyMeACoffee { get; set; }
    public string? Discord { get; set; }
    public string? Twitter { get; set; }
    public string? Facebook { get; set; }
    public string? Youtube { get; set; }
    public string? Instagram { get; set; }
    public string? Steam { get; set; }
    public string? Twitch { get; set; }
    public string? Github { get; set; }
    public string? NintendoNetwork { get; set; }
    public long EmailVisibilityId { get; set; }
    public long TimeZoneId { get; set; }
    public required string UserId {  get; set; }
    public virtual ApplicationUser? User { get; set; }
    public virtual File? Avatar {  get; set; }
    public virtual Visibility? EmailVisibility { get; set; }
    public virtual TimeZone? TimeZone { get; set; }
}
