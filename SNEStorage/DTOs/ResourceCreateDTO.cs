namespace SNEStorage.DTOs;
public class ResourceCreateDTO
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public required long VideogameId { get; set; }
    public required string Description { get; set; }
    public bool IncludesPorn { get; set; } = false;
    public bool IncludesGore { get; set; } = false;
    public bool IncludesPolitics { get; set; } = false;
    public bool IncludesSlurs { get; set; } = false;
    public bool IncludesSensitiveContent { get; set; } = false;
    public long VisibilityId { get; set; }
    public required string ChangeLog { get; set; }
    public required string Version { get; set; }
    public required IFormFile File { get; set; }
    public string[] Tags { get; set; } = [];
    public long[] Types { get; set; } = [];
    public string[] ExternalAuthors { get; set; } = [];
    public string[] AuthorsIDs { get; set; } = [];
    public long[] DependencyIDs { get; set; } = [];
    public string? RepositoryURL { get; set; }
    public ExternalDependencyDTO[] ExternalDependencies { get; set; } = [];
    public IFormFile[] Previews { get; set; } = [];
    public bool IAmAuthor { get; set; } = true;
    public bool SealOfQualityRequested { get; set; } = false;
}
