using SNEStorage.Components.About;
using SNEStorage.Components.Shared;

namespace SNEStorage.Components.Pages;

public partial class About : StaticDocumentPage<AboutDocument>
{
    protected override string DocumentPath => "Content/about.json";
    protected override string LoadErrorMessage => "About information could not be loaded. Please try again.";
}
