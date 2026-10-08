using SNEStorage.Models;

namespace SNEStorage.Components.Resources;

// Original visible values used to serialize changed fields and selection additions/removals.
internal sealed class ResourceEditorSelectionSnapshot
{
    public TagSelection[] Tags { get; init; } = [];
    public TypeSelection[] Types { get; init; } = [];
    public AuthorSelection[] Authors { get; init; } = [];
    public TagSelection[] ExternalAuthors { get; init; } = [];
    public DependencySelection[] Dependencies { get; init; } = [];
    public ExternalDependencySelection[] ExternalDependencies { get; init; } = [];
    public long[] PreviewIds { get; init; } = [];
    public string? RepositoryURL { get; init; }
    public VersionSelection? Version { get; init; }

    public static ResourceEditorSelectionSnapshot From(Resource resource, ResourceVersion? version) => new()
    {
        RepositoryURL = resource.RepositoryURL,
        Version = version is null ? null : new VersionSelection(version.Version, version.ChangeLog, version.SealOfQualityRequested),
        Tags = resource.ResourceTags
            .Where(tag => !string.IsNullOrWhiteSpace(tag.Tag?.Name))
            .Select(tag => new TagSelection(tag.Id, tag.Tag!.Name)).ToArray(),
        Types = resource.ResourceResourceTypes
            .Select(type => new TypeSelection(type.Id, type.ResourceTypeId)).ToArray(),
        Authors = resource.ResourcesAuthors.Select(author => new AuthorSelection(author.Id, author.UserId)).ToArray(),
        ExternalAuthors = resource.ResourceExternalAuthors.Select(author => new TagSelection(author.Id, author.Author)).ToArray(),
        Dependencies = resource.ResourceDependencies.Select(dependency => new DependencySelection(dependency.Id, dependency.DependencyResourceId)).ToArray(),
        ExternalDependencies = resource.ResourceExternalDependencies.Select(dependency => new ExternalDependencySelection(
            dependency.Id, dependency.DependencyName, dependency.DependencyURL, dependency.Mirror1, dependency.Mirror2, dependency.Mirror3, dependency.Mirror4)).ToArray(),
        PreviewIds = resource.ResourcePreviews.Select(preview => preview.Id).ToArray()
    };

    internal sealed record TagSelection(long Id, string Name);
    internal sealed record TypeSelection(long Id, long TypeId);
    internal sealed record AuthorSelection(long Id, string UserId);
    internal sealed record DependencySelection(long Id, long ResourceId);
    internal sealed record ExternalDependencySelection(long Id, string DependencyName, string DependencyURL,
        string? Mirror1, string? Mirror2, string? Mirror3, string? Mirror4);
    internal sealed record VersionSelection(string? Label, string? ReleaseNotes, bool SealOfQualityRequested);
}
