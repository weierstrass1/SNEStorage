namespace SNEStorage.Components.Donations;

public sealed class DonationsDocument
{
    public string Title { get; set; } = string.Empty;
    public string Kicker { get; set; } = string.Empty;
    public string Introduction { get; set; } = string.Empty;
    public string PatreonTitle { get; set; } = string.Empty;
    public string PatreonDescription { get; set; } = string.Empty;
    public string PatreonUrl { get; set; } = string.Empty;
    public string SupportLabel { get; set; } = string.Empty;
    public string LinkNote { get; set; } = string.Empty;
    public string UnavailableText { get; set; } = string.Empty;
    public string BenefitsTitle { get; set; } = string.Empty;
    public List<DonationSection> Benefits { get; set; } = [];
    public string SectionsTitle { get; set; } = string.Empty;
    public List<DonationSection> Sections { get; set; } = [];
    public string ThanksTitle { get; set; } = string.Empty;
    public string ThanksText { get; set; } = string.Empty;
    public string CommunityLinkLabel { get; set; } = string.Empty;

    public string? SafePatreonUrl => Uri.TryCreate(PatreonUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo) &&
        (uri.Host.Equals("patreon.com", StringComparison.OrdinalIgnoreCase) ||
         uri.Host.Equals("www.patreon.com", StringComparison.OrdinalIgnoreCase))
        ? uri.AbsoluteUri : null;
}

public sealed class DonationSection
{
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
