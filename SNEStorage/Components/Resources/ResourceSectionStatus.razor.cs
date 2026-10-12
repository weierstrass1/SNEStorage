using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Resources;

public partial class ResourceSectionStatus
{
    [Parameter] public bool Loading { get; set; }
    [Parameter] public int? ErrorStatus { get; set; }
    [Parameter] public string? ErrorBody { get; set; }
    [Parameter] public string Label { get; set; } = "section";
    [Parameter] public EventCallback OnRetry { get; set; }
    protected string ErrorMessage => ApiErrorPresentation.Describe(ErrorStatus ?? 0, ErrorBody, "resource").Message;
}
