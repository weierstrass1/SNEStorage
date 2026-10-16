using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Resources;

public partial class ResourceListRow
{
    private static readonly Regex HexColor = new("\\A#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})\\z", RegexOptions.CultureInvariant);
    private ResourceFinderViewDTO? _preparedItem;
    [Parameter, EditorRequired] public ResourceFinderViewDTO Item { get; set; } = default!;
    [Parameter] public bool ShowGame { get; set; } = true;
    [Parameter] public bool ShowTypes { get; set; } = true;
    [Parameter] public bool ShowCategories { get; set; } = true;
    private IReadOnlyList<ResourceListValue> Authors { get; set; } = [];
    private IReadOnlyList<ResourceListValue> Types { get; set; } = [];
    private IReadOnlyList<ResourceListValue> Categories { get; set; } = [];
    private IReadOnlyList<IdNamePair> Tags { get; set; } = [];
    private IReadOnlyList<string> Warnings { get; set; } = [];
    private string TagsId => $"library-tags-{Item.ResourceIdName.Id}";
    private string RatingWidth => $"width:{(Math.Clamp(Item.Score, 0, 5) * 20).ToString("0.##", CultureInfo.InvariantCulture)}%";
    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_preparedItem, Item)) return;
        _preparedItem = Item;
        Authors = [.. Item.AuthorsUserSummary.OrderBy(author => author.Username, StringComparer.OrdinalIgnoreCase)
            .Select(author => new ResourceListValue($"user-{author.Id}", author.Username,
            $"/Profile/{Uri.EscapeDataString(author.Id)}", author.UsernameColor is { } color && HexColor.IsMatch(color) ? color : "#dcc8fa"))
            .Concat(Item.ExternalAuthors.OrderBy(author => author.Name, StringComparer.OrdinalIgnoreCase)
                .Select(author => new ResourceListValue($"external-{author.Id}", author.Name)))];
        Types = Values(Item.Types);
        Categories = Values(Item.Categories);
        Tags = [.. Item.Tags.DistinctBy(tag => tag.Id).OrderBy(tag => tag.Name, StringComparer.OrdinalIgnoreCase)];
        Warnings = [.. new[] { Item.IncludesPorn ? "Sexual content" : null, Item.IncludesGore ? "Gore" : null,
            Item.IncludesPolitics ? "Politics" : null, Item.IncludesSlurs ? "Slurs" : null,
            Item.IncludesSensitiveContent ? "Sensitive content" : null }.OfType<string>()];
    }
    private static ResourceListValue[] Values(IEnumerable<IdNamePair> values) => [.. values.DistinctBy(value => value.Id)
        .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase).Select(value => new ResourceListValue(value.Id.ToString(), value.Name))];
    private static string Date(DateTime value) => value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
}
