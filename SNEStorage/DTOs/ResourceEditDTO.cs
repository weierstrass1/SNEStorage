using System.ComponentModel.DataAnnotations;

namespace SNEStorage.DTOs;
public class ResourceEditDTO
{
    public long Id { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Name { get; set; }
    public long? VideogameId { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Description { get; set; }
    public bool? IncludesPorn { get; set; } = null;
    public bool? IncludesGore { get; set; } = null;
    public bool? IncludesPolitics { get; set; } = null;
    public bool? IncludesSlurs { get; set; } = null;
    public bool? IncludesSensitiveContent { get; set; } = null;
    public long? VisibilityId { get; set; }
    public string[] Tags { get; set; } = [];
    public long[] Types { get; set; } = [];
    public long[] RemovePreviewIDs { get; set; } = [];
    public long[] RemoveAuthorsIDs { get; set; } = [];
    public long[] RemoveExternalAuthorsIDs { get; set; } = [];
    public long[] RemoveDependencyIDs { get; set; } = [];
    public long[] RemoveExternalDependencyIDs { get; set; } = [];
    public long[] RemoveTagIDs { get; set; } = [];
    public long[] RemovedTypeIDs { get; set; } = [];
    public string[] ExternalAuthors { get; set; } = [];
    public string[] AuthorsIDs { get; set; } = [];
    public long[] DependencyIDs { get; set; } = [];
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? RepositoryURL { get; set; }
    public ExternalDependencyDTO[] ExternalDependencies { get; set; } = [];
    public IFormFile[] Previews { get; set; } = [];
}
