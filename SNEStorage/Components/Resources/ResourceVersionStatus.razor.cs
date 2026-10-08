using Microsoft.AspNetCore.Components;
using SNEStorage.Models;

namespace SNEStorage.Components.Resources;

public partial class ResourceVersionStatus
{
    [Parameter, EditorRequired] public ResourceVersion Version { get; set; } = default!;
}
