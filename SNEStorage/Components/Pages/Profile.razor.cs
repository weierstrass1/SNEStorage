using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using SNEStorage.Models;
using SNEStorage.Components.Shared;

namespace SNEStorage.Components.Pages;

[RequireAccount]
public partial class Profile
{
    private static readonly CultureInfo ProfileCulture = CultureInfo.GetCultureInfo("en-US");
    private static readonly Regex HexColorPattern = new("\\A#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})\\z", RegexOptions.Compiled);
    private UserInfo? _profile;
    private bool _isAuthenticated;

    [CascadingParameter(Name = "VerifiedPageAccount")]
    private AccountPageContext? PageAccount { get; set; }

    protected string ProfileName => string.IsNullOrWhiteSpace(_profile?.UserSummary?.Username)
        ? "Player" : _profile.UserSummary.Username;

    protected string? ProfileUsernameColor => _profile?.UserSummary?.UsernameColor ?? _profile?.UsernameColor;

    protected string ProfileAccent =>
        ProfileUsernameColor is { } color && HexColorPattern.IsMatch(color)
            ? color
            : "#b14cff";

    protected string AvatarUrl =>
        PublicFileUrl.Resolve(_profile?.UserSummary?.Avatar?.URL ?? _profile?.Avatar?.URL, "/images/caja.png");

    protected IReadOnlyList<SocialField> SocialFields => _profile is null
        ? []
        : new SocialField?[]
        {
            CreateSocialField("Patreon", _profile.Patreon),
            CreateSocialField("PayPal", _profile.Paypal),
            CreateSocialField("Buy Me a Coffee", _profile.BuyMeACoffee),
            CreateSocialField("Discord", _profile.Discord),
            CreateSocialField("Twitter / X", _profile.Twitter),
            CreateSocialField("Facebook", _profile.Facebook),
            CreateSocialField("YouTube", _profile.Youtube),
            CreateSocialField("Instagram", _profile.Instagram),
            CreateSocialField("Steam", _profile.Steam),
            CreateSocialField("Twitch", _profile.Twitch),
            CreateSocialField("GitHub", _profile.Github),
            CreateSocialField("Nintendo Network", _profile.NintendoNetwork)
        }
        .Where(socialField => socialField is not null)
        .Select(socialField => socialField!)
        .ToArray();

    private static SocialField? CreateSocialField(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var isNintendoNetworkId = name == "Nintendo Network";
        var href = !isNintendoNetworkId &&
                   SocialProfileLinkRules.TryNormalize(name, value, out var normalized, out _)
            ? normalized
            : null;

        return new SocialField(
            name,
            SocialProfileLinkRules.GetIconClass(name),
            SocialProfileLinkRules.GetDisplayText(name, value),
            href);
    }

    protected override void OnParametersSet()
    {
        _profile = PageAccount?.Profile;
        _isAuthenticated = _profile is not null;
    }

    protected sealed record SocialField(string Name, string IconClass, string DisplayText, string? Href);
}
