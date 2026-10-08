using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Resources;

public partial class ResourceQualitySeal
{
    [Parameter] public int Size { get; set; } = 48;
    [Parameter] public string Title { get; set; } = "Meets SNEStorage's quality standards.";
}
