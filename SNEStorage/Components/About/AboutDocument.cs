namespace SNEStorage.Components.About;

// Public presentation content, loaded from the site's editable JSON file.
public sealed class AboutDocument
{
    public string Title { get; set; } = string.Empty;
    public string Kicker { get; set; } = string.Empty;
    public string Introduction { get; set; } = string.Empty;
    public string SectionsTitle { get; set; } = string.Empty;
    public List<AboutSection> Sections { get; set; } = [];
    public string CreatorTitle { get; set; } = string.Empty;
    public AboutCreator Creator { get; set; } = new();
    public string ResourcesLinkLabel { get; set; } = string.Empty;
    public string CommunityLinkLabel { get; set; } = string.Empty;
}

public sealed class AboutSection
{
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}

public sealed class AboutCreator
{
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public List<string> Paragraphs { get; set; } = [];
    public List<string> Projects { get; set; } = [];
    public List<AboutLink> Links { get; set; } = [];
}

public sealed class AboutLink
{
    public string Label { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    public string? SafeUrl => Uri.TryCreate(Url, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)
        ? uri.AbsoluteUri : null;
}
