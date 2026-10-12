using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Layout;

public partial class SideNavigation
{
    [Parameter] public bool Minimized { get; set; }
    [Parameter] public EventCallback ToggleRequested { get; set; }
}
