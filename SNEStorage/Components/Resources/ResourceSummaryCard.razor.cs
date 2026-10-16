using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Resources;

public partial class ResourceSummaryCard
{
    private static readonly Regex HexColor = new("\\A#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})\\z", RegexOptions.CultureInvariant);
    [Parameter, EditorRequired] public ResourceFinderViewDTO Item { get; set; } = default!;
    private ResourceFinderViewDTO? _preparedItem;
    private IReadOnlyList<ResourceListValue> Authors { get; set; } = [];
    private string PublicationDate => Item.CreatedAt.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
    private string? DownloadUrl => ResourceFileLinks.For(Item.ResourceIdName.Id, Item.MainVersion.Id, true);
    private string RatingLabel => Item.Votes > 0 ?
        $"★ {Item.Score.ToString("0.0", CultureInfo.InvariantCulture)}/5 · {Item.Votes.ToString("N0", CultureInfo.InvariantCulture)} {(Item.Votes == 1 ? "vote" : "votes")}" : "Not rated";

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_preparedItem, Item)) return;
        _preparedItem = Item;
        Authors = [.. Item.AuthorsUserSummary.OrderBy(author => author.Username, StringComparer.OrdinalIgnoreCase)
            .Select(author => new ResourceListValue($"user-{author.Id}", author.Username,
            $"/Profile/{Uri.EscapeDataString(author.Id)}", author.UsernameColor is { } color && HexColor.IsMatch(color) ? color : "#dcc8fa"))
            .Concat(Item.ExternalAuthors.OrderBy(author => author.Name, StringComparer.OrdinalIgnoreCase)
                .Select(author => new ResourceListValue($"external-{author.Id}", author.Name)))];
    }
}
