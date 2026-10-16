using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Resources;

public sealed record ResourceListValue(string Key, string Name, string? Href = null, string? Color = null);
public partial class ResourceListValues
{
    [Parameter] public IReadOnlyList<ResourceListValue> Items { get; set; } = [];
    [Parameter] public int Limit { get; set; } = 3;
    [Parameter] public bool Inline { get; set; }
    private readonly string _itemsId = $"resource-values-{Guid.NewGuid():N}";
}
