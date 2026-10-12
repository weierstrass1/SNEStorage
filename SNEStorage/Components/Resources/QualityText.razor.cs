using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Resources;

public partial class QualityText
{
    [Parameter, EditorRequired] public string Text { get; set; } = string.Empty;
}
