using SNEStorage.Components.Rules;
using SNEStorage.Components.Shared;

namespace SNEStorage.Components.Pages;

public partial class SealOfQuality : StaticDocumentPage<SiteRulesDocument>
{
    protected override string DocumentPath => "content/seal-of-quality.json";
    protected override string LoadErrorMessage => "The Seal of Quality guide could not be loaded. Please try again.";
    protected override void OnDocumentLoaded(SiteRulesDocument document) => document.NumberSections();
}
