using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Resources;

// Dropdown state and focus are managed by resource-pickers.js in the browser.
public partial class ResourcePicker
{
    [Parameter, EditorRequired] public string PickerId { get; set; } = string.Empty;
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;
    [Parameter] public bool Required { get; set; }
    [Parameter] public bool OpenOnHover { get; set; }
    [Parameter] public bool FocusSearch { get; set; }
    [Parameter] public RenderFragment? SummaryContent { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
