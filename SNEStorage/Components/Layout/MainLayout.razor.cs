using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Layout;

public partial class MainLayout
{
    private bool _sidebarMinimized;

    private void ToggleSidebar() => _sidebarMinimized = !_sidebarMinimized;

    [CascadingParameter(Name = "CurrentPageRoute")]
    private Microsoft.AspNetCore.Components.RouteData? CurrentPageRoute { get; set; }
}
