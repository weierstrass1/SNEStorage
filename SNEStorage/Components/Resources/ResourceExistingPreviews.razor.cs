using Microsoft.AspNetCore.Components;
using SNEStorage.Models;

namespace SNEStorage.Components.Resources;

public partial class ResourceExistingPreviews
{
    [Parameter] public long ResourceId { get; set; }
    [Parameter] public string ResourceName { get; set; } = "Resource";
    [Parameter] public IReadOnlyList<ResourcePreview> Previews { get; set; } = [];
    [Parameter] public long[] Values { get; set; } = [];
    [Parameter] public EventCallback<long[]> ValuesChanged { get; set; }
    [Parameter] public bool LoadMedia { get; set; }

    private IReadOnlyList<ResourcePreview> KeptPreviews => [.. Previews.Where(preview => Values.Contains(preview.Id))];
    private static string FileName(ResourcePreview preview) =>
        preview.File?.URL is { Length: > 0 } path ? Path.GetFileName(path.Replace('\\', '/')) : $"Preview #{preview.Id}";
    private Task ToggleAsync(long id) => ValuesChanged.InvokeAsync(Values.Contains(id)
        ? [.. Values.Where(value => value != id)] : [.. Values, id]);
}
