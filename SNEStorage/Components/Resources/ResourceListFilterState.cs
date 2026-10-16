using System.Globalization;
using System.Text.Json;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Resources;

// Editable UI state; the controller owns filtering and visibility decisions.
public sealed class ResourceListFilterState
{
    public ResourceFinderDTO Finder { get; set; } = new();
    public string? AuthorLike { get; set; }
    public SelectOptionDto? Game { get; set; }
    public long? GameId
    {
        get => Finder.VideogameIDs.FirstOrDefault() is var id && id != 0 ? id : null;
        set { Finder.VideogameIDs = value is { } id ? [id] : []; if (value is null) Game = null; }
    }

    public ResourceListFilterState Copy() => new()
    {
        Finder = JsonSerializer.Deserialize<ResourceFinderDTO>(JsonSerializer.Serialize(Finder))!,
        AuthorLike = AuthorLike?.Trim(), Game = Game
    };

    public string? Validate()
    {
        if (Finder.NameLike?.Length > TextLimits.RESOURCE_NAME_SEARCH)
            return $"Search resource names with up to {TextLimits.RESOURCE_NAME_SEARCH} characters.";
        if (AuthorLike?.Length > TextLimits.SEARCH)
            return $"Search authors with up to {TextLimits.SEARCH} characters.";
        if (Finder.TagLike?.Length > TextLimits.TAG)
            return $"Search tags with up to {TextLimits.TAG} characters.";

        if (Finder.CreatedAtFrom?.Date > Finder.CreatedAtTo?.Date ||
            Finder.VersionCreatedAtFrom?.Date > Finder.VersionCreatedAtTo?.Date)
            return "The start date must be on or before the end date.";
        if (Finder.CreatedAtTo?.Date == DateTime.MaxValue.Date ||
            Finder.VersionCreatedAtTo?.Date == DateTime.MaxValue.Date)
            return "Choose an end date earlier than December 31, 9999.";
        if (Finder.ScoreLowerLimit is < 1 or > 5 || Finder.ScoreHigherLimit is < 1 or > 5)
            return "Ratings must be between 1 and 5.";
        if (Finder.ScoreLowerLimit > Finder.ScoreHigherLimit)
            return "The minimum rating must not exceed the maximum rating.";
        return null;
    }

    public IEnumerable<string> Summary(IReadOnlyList<ResourceTypeDTO> types, IReadOnlyList<IdNamePair> categories)
    {
        if (!string.IsNullOrWhiteSpace(Finder.NameLike)) yield return $"Name: {Finder.NameLike.Trim()}";
        if (!string.IsNullOrWhiteSpace(AuthorLike)) yield return $"Author: {AuthorLike}";
        if (!string.IsNullOrWhiteSpace(Finder.TagLike)) yield return $"Tag: {Finder.TagLike.Trim()}";
        if (GameId is not null) yield return $"Game: {Game?.Name ?? "Selected game"}";
        foreach (var name in types.Where(t => Finder.TypeIds.Contains(t.Id)).Select(t => t.Name)) yield return $"Type: {name}";
        foreach (var categoryId in Finder.CategoryIDs)
            yield return $"Category: {categories.FirstOrDefault(c => c.Id == categoryId)?.Name ?? "Selected category"}";
        if (Finder.CreatedAtFrom is { } from) yield return $"Created from {Date(from)}";
        if (Finder.CreatedAtTo is { } to) yield return $"Created through {Date(to)}";
        if (Finder.VersionCreatedAtFrom is { } releaseFrom) yield return $"Release from {Date(releaseFrom)}";
        if (Finder.VersionCreatedAtTo is { } releaseTo) yield return $"Release through {Date(releaseTo)}";
        if (Finder.ScoreLowerLimit is { } min) yield return $"Rating ≥ {min}";
        if (Finder.ScoreHigherLimit is { } max) yield return $"Rating ≤ {max}";
        if (Finder.SealOfQuality is { } seal) yield return seal ? "With Seal of Quality" : "Without Seal of Quality";
        if (Finder.MustHaveScore is { } rated) yield return rated ? "Rated resources" : "Unrated resources";
    }
    private static string Date(DateTime value) => value.ToString("MMM d, yyyy", CultureInfo.InvariantCulture);
}
