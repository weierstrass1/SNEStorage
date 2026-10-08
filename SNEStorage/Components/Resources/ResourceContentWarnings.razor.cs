using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Resources;

public partial class ResourceContentWarnings
{
    [Parameter] public bool IncludesPorn { get; set; }
    [Parameter] public EventCallback<bool> IncludesPornChanged { get; set; }
    [Parameter] public bool IncludesGore { get; set; }
    [Parameter] public EventCallback<bool> IncludesGoreChanged { get; set; }
    [Parameter] public bool IncludesPolitics { get; set; }
    [Parameter] public EventCallback<bool> IncludesPoliticsChanged { get; set; }
    [Parameter] public bool IncludesSlurs { get; set; }
    [Parameter] public EventCallback<bool> IncludesSlursChanged { get; set; }
    [Parameter] public bool IncludesSensitiveContent { get; set; }
    [Parameter] public EventCallback<bool> IncludesSensitiveContentChanged { get; set; }
}
