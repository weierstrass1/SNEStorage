using SNEStorage.Components.About;
using SNEStorage.Components.Shared;
using SNEStorage.Models;
using System.Text.RegularExpressions;

namespace SNEStorage.Components.Pages;

public partial class About : StaticDocumentPage<AboutDocument>
{
    protected override string DocumentPath => "Content/about.json";
    protected override string LoadErrorMessage => "About information could not be loaded. Please try again.";
    private static readonly Regex HexColor = new("\\A#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})\\z", RegexOptions.Compiled);
    private string CreatorProfileUrl => $"/Profile/{Uri.EscapeDataString(_document?.Creator.UserId ?? string.Empty)}";
    private static string CreatorColor(UserInfo profile) =>
        (profile.UserSummary?.UsernameColor ?? profile.UsernameColor) is { } color && HexColor.IsMatch(color) ? color : "#e7e2ff";
    private static string CreatorAvatar(UserInfo profile) =>
        PublicFileUrl.Resolve(profile.UserSummary?.Avatar?.URL ?? profile.Avatar?.URL, "/images/caja.png");
}
