using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Resources;

public sealed record ResourceListValue(string Key, string Name, string? Href = null, string? Color = null);
public partial class ResourceListValues
{
    [Parameter] public IReadOnlyList<ResourceListValue> Items { get; set; } = [];
    [Parameter] public int Limit { get; set; } = 3;
    private bool _expanded;
    private IEnumerable<ResourceListValue> Visible => _expanded ? Items : Items.Take(Limit);
    private void Toggle() => _expanded = !_expanded;
}
