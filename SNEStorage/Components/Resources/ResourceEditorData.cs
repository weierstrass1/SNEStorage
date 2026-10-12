using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Components.Resources;

// Independent read responses used only to fill the editor and its original selections.
internal sealed class ResourceEditorData
{
    public IReadOnlyList<ResourceAuthorDTO> Authors { get; set; } = [];
    public IReadOnlyList<ResourceExternalAuthor> ExternalAuthors { get; set; } = [];
    public IReadOnlyList<ResourceDependencyViewDTO> Dependencies { get; set; } = [];
    public IReadOnlyList<ResourceExternalDependency> ExternalDependencies { get; set; } = [];
    public IReadOnlyList<ResourcePreview> Previews { get; set; } = [];
    public IReadOnlyList<ResourceVersion> Versions { get; set; } = [];
}
