using Microsoft.AspNetCore.Components;
using SNEStorage.Components.Shared;

namespace SNEStorage.Components.Pages;

[RequireAccount(ResourceManagement = true)]
public partial class ResourceVersionEdit
{
    [Parameter] public long ResourceId { get; set; }
    [Parameter] public long VersionId { get; set; }
}
