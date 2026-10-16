using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Layout;

public partial class SideNavigation
{
    private bool _infoExpanded, _communityExpanded, _toolsExpanded;
    [Parameter] public bool Minimized { get; set; }
    [Parameter] public EventCallback ToggleRequested { get; set; }
}
