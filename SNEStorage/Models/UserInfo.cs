using System.ComponentModel.DataAnnotations;
using SNEStorage.DTOs;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace SNEStorage.Models;

public partial class UserInfo
{
    public long Id { get; set; }
    [StringLength(7)]
    [RegularExpression(@"\A#[0-9A-Fa-f]{6}\z")]
    public string? UsernameColor { get; set; }
    [StringLength(100)]
    public string? Title { get; set; }
    public long AvatarId { get; set; }
    public DateOnly Birthday { get; set; }
    public string? Bio { get; set; }
    [StringLength(100)]
    public string? Patreon { get; set; }
    [StringLength(100)]
    public string? Paypal { get; set; }
    [StringLength(100)]
    public string? BuyMeACoffee { get; set; }
    [StringLength(100)]
    public string? Discord { get; set; }
    [StringLength(100)]
    public string? Twitter { get; set; }
    [StringLength(100)]
    public string? Facebook { get; set; }
    [StringLength(100)]
    public string? Youtube { get; set; }
    [StringLength(100)]
    public string? Instagram { get; set; }
    [StringLength(100)]
    public string? Steam { get; set; }
    [StringLength(100)]
    public string? Twitch { get; set; }
    [StringLength(100)]
    public string? Github { get; set; }
    [StringLength(100)]
    public string? NintendoNetwork { get; set; }
    public long EmailVisibilityId { get; set; }
    public long TimeZoneId { get; set; }
    [Required(AllowEmptyStrings = true)]
    [StringLength(450)]
    public required string UserId { get; set; }
    [StringLength(20)]
    public string? DiscordUserId { get; set; }
    [StringLength(32)]
    public string? DiscordUsername { get; set; }
    public DateTime? DiscordLinkedAt { get; set; }
    [JsonIgnore]
    public virtual ApplicationUser? User { get; set; }
    [NotMapped]
    public UserNameDTO? UserSummary { get; set; }
    public virtual File? Avatar { get; set; }
    public virtual Visibility? EmailVisibility { get; set; }
    public virtual TimeZone? TimeZone { get; set; }
}
