using SNEStorage.Components.About;

namespace SNEStorage.Components.Credits;

public sealed class CreditsDocument
{
    public string Title { get; set; } = string.Empty;
    public string Introduction { get; set; } = string.Empty;
    public AboutCreator Creator { get; set; } = new();
    public List<CreditsSection> Sections { get; set; } = [];
}

public sealed class CreditsSection
{
    public string Title { get; set; } = string.Empty;
    public List<CreditEntry> Entries { get; set; } = [];
}

public sealed class CreditEntry
{
    public string Title { get; set; } = string.Empty;
    public string By { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string License { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public string ImageAlt { get; set; } = string.Empty;
    public string? SafeImageUrl => !string.IsNullOrWhiteSpace(Image) && Image.StartsWith("/images/", StringComparison.Ordinal) &&
        !Image.Contains('\\') && !Image.Contains("..", StringComparison.Ordinal) ? Image : null;
    public List<AboutLink> Links { get; set; } = [];
}
