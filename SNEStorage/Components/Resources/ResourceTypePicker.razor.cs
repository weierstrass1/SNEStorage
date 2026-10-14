using Microsoft.AspNetCore.Components;
using SNEStorage.DTOs;
using System.Text.RegularExpressions;

namespace SNEStorage.Components.Resources;

public partial class ResourceTypePicker
{
    [Parameter] public IReadOnlyList<ResourceTypeDTO> Options { get; set; } = [];
    [Parameter] public long[] Values { get; set; } = [];
    [Parameter] public EventCallback<long[]> ValuesChanged { get; set; }
    [Parameter] public bool Required { get; set; } = true;
    [Parameter] public string Placeholder { get; set; } = "Choose resource types";

    private IReadOnlyList<TypeCategory> Categories { get; set; } = [];
    protected override void OnParametersSet() => Categories = [.. Options
        .GroupBy(option => option.CategoryId)
        .Select(group => new TypeCategory(group.Key,
            group.Select(option => option.CategoryName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "Other types",
            [.. group.OrderBy(option => Regex.IsMatch(option.Name, @"\bOther\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).ThenBy(option => option.Name, StringComparer.OrdinalIgnoreCase)]))
        .OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase)];

    private int SelectedCount(TypeCategory category) => category.Options.Count(option => Values.Contains(option.Id));

    protected string SelectedNames => Values.Length == 0
        ? Placeholder
        : string.Join(", ", Options.Where(option => Values.Contains(option.Id)).Select(option => option.Name));

    protected Task SelectionChangedAsync(long id, ChangeEventArgs args) => ValuesChanged.InvokeAsync(
        args.Value is true ? [.. Values.Append(id).Distinct()] : [.. Values.Where(value => value != id)]);

    private sealed record TypeCategory(long Id, string Name, IReadOnlyList<ResourceTypeDTO> Options);
}
