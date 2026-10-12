namespace SNEStorage.Components.Rules;

// Presentation data for the public JSON document, independent of database models.
public sealed class SiteRulesDocument
{
    public string Title { get; set; } = string.Empty;
    public string Introduction { get; set; } = string.Empty;
    public string Kicker { get; set; } = string.Empty;
    public string RelatedLinkLabel { get; set; } = string.Empty;
    public string UpdatedLabel { get; set; } = string.Empty;
    public string UpdatedOn { get; set; } = string.Empty;
    public string NavigationTitle { get; set; } = string.Empty;
    public string ContextTitle { get; set; } = string.Empty;
    public string ContextDescription { get; set; } = string.Empty;
    public List<SiteRulesSection> Sections { get; set; } = [];

    public void NumberSections() => NumberSections(Sections);

    public static void NumberSections(IReadOnlyList<SiteRulesSection> sections)
    {
        for (var index = 0; index < sections.Count; index++)
        {
            var section = sections[index];
            var letter = string.Empty;
            for (var position = index + 1; position > 0; position = (position - 1) / 26)
                letter = (char)('A' + (position - 1) % 26) + letter;
            section.Letter = letter;
            var number = 1;
            foreach (var rule in section.Rules.Concat(section.Groups.SelectMany(group => group.Rules)))
                rule.Number = number++;
        }
    }
}

public sealed class SiteRulesSection
{
    public string Letter { get; set; } = string.Empty;
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<SiteRuleEntry> Rules { get; set; } = [];
    public List<SiteRulesGroup> Groups { get; set; } = [];
}

public sealed class SiteRulesGroup
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<SiteRuleEntry> Rules { get; set; } = [];
}

public sealed class SiteRuleEntry
{
    public string Id { get; set; } = string.Empty;
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
}
