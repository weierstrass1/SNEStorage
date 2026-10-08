using Microsoft.AspNetCore.Components;
using System.Text.RegularExpressions;

namespace SNEStorage.Components.Layout;

public partial class AccountMenu
{
    private const string DefaultAvatar = "/images/caja.png";
    private static readonly Regex HexColorPattern = new("\\A#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})\\z", RegexOptions.Compiled);
    private string? _failedAvatarUrl;

    [Parameter] public string Username { get; set; } = "Account";
    [Parameter] public string? UsernameColor { get; set; }
    [Parameter] public string? AvatarUrl { get; set; }

    protected string DisplayAvatarUrl => string.IsNullOrWhiteSpace(AvatarUrl) || AvatarUrl == _failedAvatarUrl
        ? DefaultAvatar : AvatarUrl;

    protected string DisplayUsernameColor => UsernameColor is { } color && HexColorPattern.IsMatch(color)
        ? color : "#e7e2ff";

    protected void ShowFallbackAvatar() => _failedAvatarUrl = AvatarUrl;
}
