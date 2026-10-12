using SNEStorage.Components.Community;
using SNEStorage.Components.Shared;

namespace SNEStorage.Components.Pages;

public partial class Community : StaticDocumentPage<CommunityDocument>
{
    protected override string DocumentPath => "Content/community.json";
    protected override string LoadErrorMessage => "Community information could not be loaded. Please try again.";
}
