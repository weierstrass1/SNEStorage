namespace SNEStorage.Components.Community;

// Presentation content from a public JSON file; independent of the backend.
public sealed class CommunityDocument
{
    public string Title { get; set; } = string.Empty;
    public string Kicker { get; set; } = string.Empty;
    public string Introduction { get; set; } = string.Empty;
    public string DiscordTitle { get; set; } = string.Empty;
    public string DiscordDescription { get; set; } = string.Empty;
    public string DiscordUrl { get; set; } = string.Empty;
    public string JoinLabel { get; set; } = string.Empty;
    public string LinkNote { get; set; } = string.Empty;
    public string UnavailableText { get; set; } = string.Empty;
    public string SectionsTitle { get; set; } = string.Empty;
    public List<CommunitySection> Sections { get; set; } = [];
    public string GuidelinesText { get; set; } = string.Empty;
    public string GuidelinesLinkLabel { get; set; } = string.Empty;

    public string? SafeDiscordUrl => Uri.TryCreate(DiscordUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)
        ? uri.AbsoluteUri : null;
}

public sealed class CommunitySection
{
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
