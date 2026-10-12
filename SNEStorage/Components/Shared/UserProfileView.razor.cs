using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using SNEStorage.Models;
using SNEStorage.Components.Pages;

namespace SNEStorage.Components.Shared;

public partial class UserProfileView
{
    private static readonly Regex HexColorPattern = new("\\A#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})\\z", RegexOptions.Compiled);
    [Parameter, EditorRequired] public UserInfo? Profile { get; set; }
    [Parameter] public bool IsOwnProfile { get; set; }

    protected string ProfileName => string.IsNullOrWhiteSpace(Profile?.UserSummary?.Username)
        ? "Player" : Profile.UserSummary.Username;

    protected string? ProfileUsernameColor => Profile?.UserSummary?.UsernameColor ?? Profile?.UsernameColor;

    protected string ProfileAccent =>
        ProfileUsernameColor is { } color && HexColorPattern.IsMatch(color)
            ? color
            : "#b14cff";

    protected string AvatarUrl =>
        PublicFileUrl.Resolve(Profile?.UserSummary?.Avatar?.URL ?? Profile?.Avatar?.URL, "/images/caja.png");

    protected IReadOnlyList<SocialField> SocialFields => Profile is null
        ? []
        : new SocialField?[]
        {
            CreateSocialField("Patreon", Profile.Patreon),
            CreateSocialField("PayPal", Profile.Paypal),
            CreateSocialField("Buy Me a Coffee", Profile.BuyMeACoffee),
            CreateSocialField("Discord", Profile.Discord),
            CreateSocialField("Twitter / X", Profile.Twitter),
            CreateSocialField("Facebook", Profile.Facebook),
            CreateSocialField("YouTube", Profile.Youtube),
            CreateSocialField("Instagram", Profile.Instagram),
            CreateSocialField("Steam", Profile.Steam),
            CreateSocialField("Twitch", Profile.Twitch),
            CreateSocialField("GitHub", Profile.Github),
            CreateSocialField("Nintendo Network", Profile.NintendoNetwork)
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

        var href = SocialProfileLinkRules.TryNormalize(name, value, out var normalized, out _) &&
                   Uri.TryCreate(normalized, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps
            ? normalized
            : null;

        return new SocialField(
            name,
            SocialProfileLinkRules.GetIconClass(name),
            SocialProfileLinkRules.GetDisplayText(name, value),
            href);
    }

    protected sealed record SocialField(string Name, string IconClass, string DisplayText, string? Href);
}
