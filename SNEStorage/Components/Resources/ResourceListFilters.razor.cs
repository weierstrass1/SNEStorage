using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Resources;

public partial class ResourceListFilters
{
    [Parameter, EditorRequired] public ResourceListFilterState State { get; set; } = new();
    [Parameter] public IReadOnlyList<ResourceTypeDTO> Types { get; set; } = [];
    [Parameter] public EventCallback OnApply { get; set; }
    [Parameter] public EventCallback OnReset { get; set; }
    [Parameter] public string? Error { get; set; }
    [Parameter] public bool Disabled { get; set; }
    private IReadOnlyList<IdNamePair> Categories { get; set; } = [];
    // InputSelect<bool?> formats null as "false"; explicit UI values preserve the Any option.
    private string SealSelection
    {
        get => Selection(State.Finder.SealOfQuality);
        set => State.Finder.SealOfQuality = Selection(value);
    }
    private string RatingSelection
    {
        get => Selection(State.Finder.MustHaveScore);
        set => State.Finder.MustHaveScore = Selection(value);
    }
    private static string Selection(bool? value) => value switch { true => "true", false => "false", null => "any" };
    private static bool? Selection(string value) => value switch { "true" => true, "false" => false, _ => null };
    private string CategoryLabel => State.Finder.CategoryIDs.Length == 0 ? "All categories" :
        string.Join(", ", Categories.Where(c => State.Finder.CategoryIDs.Contains(c.Id)).Select(c => c.Name));
    protected override void OnParametersSet() => Categories = Types
        .GroupBy(type => type.CategoryId).Select(group => new IdNamePair { Id = group.Key, Name = group.First().CategoryName })
        .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase).ToArray();
    private void ToggleCategory(long id, ChangeEventArgs args) => State.Finder.CategoryIDs = args.Value is true
        ? State.Finder.CategoryIDs.Append(id).Distinct().ToArray() : State.Finder.CategoryIDs.Where(value => value != id).ToArray();
    private void SelectGame(SelectOptionDto game) => State.Game = game;
    private void ClearGame() => State.GameId = null;
    private Task ApplyAsync(EditContext context) => OnApply.InvokeAsync();
}
