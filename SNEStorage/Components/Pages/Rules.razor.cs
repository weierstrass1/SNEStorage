using SNEStorage.Components.Rules;
using SNEStorage.Components.Shared;

namespace SNEStorage.Components.Pages;

public partial class Rules : StaticDocumentPage<SiteRulesDocument>
{
    protected override string DocumentPath => "Content/site-rules.json";
    protected override string LoadErrorMessage => "Site rules could not be loaded. Please try again.";
    protected override void OnDocumentLoaded(SiteRulesDocument document) => document.NumberSections();
}
