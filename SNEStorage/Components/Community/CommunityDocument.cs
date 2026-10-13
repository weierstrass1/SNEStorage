namespace SNEStorage.Components.Community;

// Presentation content from a public JSON file; independent of the backend.
public sealed class CommunityDocument
{
    public string Title { get; set; } = string.Empty;
    public string Kicker { get; set; } = string.Empty;
    public string Introduction { get; set; } = string.Empty;
    public List<CommunityServer> Servers { get; set; } = [];
    public string LinkNote { get; set; } = string.Empty;
    public string UnavailableText { get; set; } = string.Empty;
    public string ConnectionTitle { get; set; } = string.Empty;
    public string ConnectionIntroduction { get; set; } = string.Empty;
    public List<CommunityConnectionGuide> ConnectionGuides { get; set; } = [];
    public string SectionsTitle { get; set; } = string.Empty;
    public List<CommunitySection> Sections { get; set; } = [];
    public string GuidelinesText { get; set; } = string.Empty;
    public string GuidelinesLinkLabel { get; set; } = string.Empty;

}

public sealed class CommunityServer
{
    public string Name { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DiscordUrl { get; set; } = string.Empty;
    public string LogoUrl { get; set; } = string.Empty;
    public string JoinLabel { get; set; } = string.Empty;
    public bool Primary { get; set; }

    public string? SafeDiscordUrl => Uri.TryCreate(DiscordUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)
        && uri.Host is "discord.gg" or "discord.com" or "discordapp.com"
        ? uri.AbsoluteUri : null;
}

public sealed class CommunitySection
{
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public sealed class CommunityConnectionGuide
{
    public string Title { get; set; } = string.Empty;
    public List<CommunityConnectionStep> Steps { get; set; } = [];
}

public sealed class CommunityConnectionStep
{
    public string Text { get; set; } = string.Empty;
    public string LinkLabel { get; set; } = string.Empty;
    public string LinkUrl { get; set; } = string.Empty;

    public string? SafeLinkUrl => LinkUrl is "/Profile/me" or "/Profile/Edit" ? LinkUrl :
        Uri.TryCreate(LinkUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) &&
        uri.Host is "discord.gg" or "discord.com" or "discordapp.com" ? uri.AbsoluteUri : null;
}
