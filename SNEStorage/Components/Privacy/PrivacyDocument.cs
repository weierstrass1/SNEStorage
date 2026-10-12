using SNEStorage.Components.Rules;

namespace SNEStorage.Components.Privacy;

// Public presentation content loaded from JSON; not an EF model or backend DTO.
public sealed class PrivacyDocument
{
    public string Title { get; set; } = string.Empty;
    public string Kicker { get; set; } = string.Empty;
    public string Introduction { get; set; } = string.Empty;
    public string UpdatedLabel { get; set; } = string.Empty;
    public string UpdatedOn { get; set; } = string.Empty;
    public string NavigationTitle { get; set; } = string.Empty;
    public string SummaryTitle { get; set; } = string.Empty;
    public string SummaryText { get; set; } = string.Empty;
    public string ContactTitle { get; set; } = string.Empty;
    public PrivacyDocumentLink Contact { get; set; } = new();
    public string ProviderLinksTitle { get; set; } = string.Empty;
    public List<PrivacyDocumentLink> ProviderLinks { get; set; } = [];
    public List<SiteRulesSection> Sections { get; set; } = [];

    public void NumberSections() => SiteRulesDocument.NumberSections(Sections);
}

public sealed class PrivacyDocumentLink
{
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string? SafeUrl => Uri.TryCreate(Url, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeMailto)
        ? uri.AbsoluteUri : null;
}
