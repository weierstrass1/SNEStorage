using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Layout;

public partial class MainLayout
{
    [CascadingParameter(Name = "CurrentPageRoute")]
    private Microsoft.AspNetCore.Components.RouteData? CurrentPageRoute { get; set; }
}
