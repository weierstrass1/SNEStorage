using Microsoft.AspNetCore.Components;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Resources;

public partial class ResourceTypePicker
{
    [Parameter] public IReadOnlyList<ResourceTypeDTO> Options { get; set; } = [];
    [Parameter] public long[] Values { get; set; } = [];
    [Parameter] public EventCallback<long[]> ValuesChanged { get; set; }

    private IReadOnlyList<TypeCategory> Categories { get; set; } = [];
    protected override void OnParametersSet() => Categories = Options
        .GroupBy(option => option.CategoryId)
        .Select(group => new TypeCategory(group.Key,
            group.Select(option => option.CategoryName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "Other types",
            group.OrderBy(option => option.Name, StringComparer.OrdinalIgnoreCase).ToArray()))
        .OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase).ToArray();

    private int SelectedCount(TypeCategory category) => category.Options.Count(option => Values.Contains(option.Id));

    protected string SelectedNames => Values.Length == 0
        ? "Choose resource types"
        : string.Join(", ", Options.Where(option => Values.Contains(option.Id)).Select(option => option.Name));

    protected Task SelectionChangedAsync(long id, ChangeEventArgs args) => ValuesChanged.InvokeAsync(
        args.Value is true ? Values.Append(id).Distinct().ToArray() : Values.Where(value => value != id).ToArray());

    private sealed record TypeCategory(long Id, string Name, IReadOnlyList<ResourceTypeDTO> Options);
}
