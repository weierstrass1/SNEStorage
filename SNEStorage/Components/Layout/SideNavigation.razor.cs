using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Layout;

public partial class SideNavigation
{
    private bool _infoExpanded;
    [Parameter] public bool Minimized { get; set; }
    [Parameter] public EventCallback ToggleRequested { get; set; }
}
