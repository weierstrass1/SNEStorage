using SNEStorage.DTOs;

namespace SNEStorage.Components.Pages;

// Form state only. Files are sent directly by the browser to ResourceController.
internal sealed class ResourceSubmissionFields
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public long? VideogameId { get; set; }
    public long VisibilityId { get; set; }
    public string Version { get; set; } = "1.0.0";
    public string ChangeLog { get; set; } = string.Empty;
    public string? RepositoryURL { get; set; }
    public bool IAmAuthor { get; set; } = true;
    public bool SealOfQualityRequested { get; set; }
    public bool IncludesPorn { get; set; }
    public bool IncludesGore { get; set; }
    public bool IncludesPolitics { get; set; }
    public bool IncludesSlurs { get; set; }
    public bool IncludesSensitiveContent { get; set; }
    public string[] Tags { get; set; } = [];
    public long[] Types { get; set; } = [];
    public string[] AuthorsIDs { get; set; } = [];
    public string[] ExternalAuthors { get; set; } = [];
    public long[] DependencyIDs { get; set; } = [];
    public ExternalDependencyDTO[] ExternalDependencies { get; set; } = [];
}
