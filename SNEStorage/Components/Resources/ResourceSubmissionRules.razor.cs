using SNEStorage.Components.Rules;

namespace SNEStorage.Components.Resources;

public partial class ResourceSubmissionRules
{
    private static readonly string[] SectionIds =
        ["safety-and-authorship", "resources", "visibility-and-permissions"];
    private SiteRulesSection[] _sections = [];

    protected override string DocumentPath => "Content/site-rules.json";
    protected override string LoadErrorMessage => "The guidelines could not be loaded. Open the site rules below before uploading.";

    protected override void OnDocumentLoaded(SiteRulesDocument document)
    {
        _sections = document.Sections
            .Where(section => SectionIds.Contains(section.Id))
            .OrderBy(section => Array.IndexOf(SectionIds, section.Id))
            .ToArray();
    }
}
