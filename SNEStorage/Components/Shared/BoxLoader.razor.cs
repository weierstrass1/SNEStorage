using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Shared;

public partial class BoxLoader
{
    private static readonly string[] _colors = ["blue", "red", "yellow", "green"];

    [Parameter] public string Label { get; set; } = "Loading…";
    [Parameter] public bool Compact { get; set; }
}
