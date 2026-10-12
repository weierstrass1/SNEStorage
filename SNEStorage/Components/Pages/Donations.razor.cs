using SNEStorage.Components.Donations;
using SNEStorage.Components.Shared;

namespace SNEStorage.Components.Pages;

public partial class Donations : StaticDocumentPage<DonationsDocument>
{
    protected override string DocumentPath => "Content/donations.json";
    protected override string LoadErrorMessage => "Donation information could not be loaded. Please try again.";
}
