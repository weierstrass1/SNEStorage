using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Shared;

public partial class MarkdownText
{
    [Parameter] public string? Text { get; set; }
    [Parameter] public bool Compact { get; set; }
    [Parameter] public int? MaxLines { get; set; }
    private bool IsExcerpt => MaxLines is > 0;
}
