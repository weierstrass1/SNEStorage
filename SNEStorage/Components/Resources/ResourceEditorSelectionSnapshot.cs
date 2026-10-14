using SNEStorage.Models;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Resources;

// Original visible values used to serialize changed fields and selection additions/removals.
internal sealed class ResourceEditorSelectionSnapshot
{
    public TagSelection[] Tags { get; init; } = [];
    public TypeSelection[] Types { get; init; } = [];
    public AuthorSelection[] Authors { get; init; } = [];
    public string? CurrentUserId { get; init; }
    public TagSelection[] ExternalAuthors { get; init; } = [];
    public DependencySelection[] Dependencies { get; init; } = [];
    public ExternalDependencySelection[] ExternalDependencies { get; init; } = [];
    public long[] PreviewIds { get; init; } = [];
    public string? RepositoryURL { get; init; }
    public VersionSelection? Version { get; init; }

    public static ResourceEditorSelectionSnapshot From(ResourceReadDTO resource, ResourceVersion? version, ResourceEditorData data, string userId) => new()
    {
        RepositoryURL = resource.RepositoryURL,
        CurrentUserId = userId,
        Version = version is null ? null : new VersionSelection(version.Version, version.ChangeLog, version.SealOfQualityRequested),
        Tags = [.. resource.Tags
            .Where(tag => !string.IsNullOrWhiteSpace(tag.Name))
            .Select(tag => new TagSelection(tag.Id, tag.Name))],
        Types = [.. resource.Types.Select(type => new TypeSelection(type.Id, type.ResourceTypeId))],
        Authors = [.. data.Authors.Select(author => new AuthorSelection(author.Id, author.UserSummary.Id))],
        ExternalAuthors = [.. data.ExternalAuthors.Select(author => new TagSelection(author.Id, author.Author))],
        Dependencies = [.. data.Dependencies.Select(dependency => new DependencySelection(dependency.Id, dependency.DependencyResourceId))],
        ExternalDependencies = [.. data.ExternalDependencies.Select(dependency => new ExternalDependencySelection(
            dependency.Id, dependency.DependencyName, dependency.DependencyURL, dependency.Mirror1, dependency.Mirror2, dependency.Mirror3, dependency.Mirror4))],
        PreviewIds = [.. data.Previews.Select(preview => preview.Id)]
    };

    internal sealed record TagSelection(long Id, string Name);
    internal sealed record TypeSelection(long Id, long TypeId);
    internal sealed record AuthorSelection(long Id, string UserId);
    internal sealed record DependencySelection(long Id, long ResourceId);
    internal sealed record ExternalDependencySelection(long Id, string DependencyName, string DependencyURL,
        string? Mirror1, string? Mirror2, string? Mirror3, string? Mirror4);
    internal sealed record VersionSelection(string? Label, string? ReleaseNotes, bool SealOfQualityRequested);
}
