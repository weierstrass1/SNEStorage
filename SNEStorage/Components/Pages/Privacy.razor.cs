using SNEStorage.Components.Privacy;
using SNEStorage.Components.Shared;

namespace SNEStorage.Components.Pages;

public partial class Privacy : StaticDocumentPage<PrivacyDocument>
{
    protected override string DocumentPath => "Content/privacy-policy.json";
    protected override string LoadErrorMessage => "The privacy policy could not be loaded. Please try again.";
    protected override void OnDocumentLoaded(PrivacyDocument document) => document.NumberSections();
}
