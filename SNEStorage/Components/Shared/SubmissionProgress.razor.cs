using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Shared;

public partial class SubmissionProgress
{
    [Parameter, EditorRequired] public string FormId { get; set; } = string.Empty;
    [Parameter] public string Title { get; set; } = "Submitting resource…";
}
