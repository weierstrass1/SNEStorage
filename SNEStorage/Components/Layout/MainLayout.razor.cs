using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Layout;

public partial class MainLayout
{
    private bool ShouldFocusPageTitle =>
        CurrentPageRoute?.PageType != typeof(SNEStorage.Components.Pages.Home);

    [CascadingParameter(Name = "CurrentPageRoute")]
    private Microsoft.AspNetCore.Components.RouteData? CurrentPageRoute { get; set; }
}
