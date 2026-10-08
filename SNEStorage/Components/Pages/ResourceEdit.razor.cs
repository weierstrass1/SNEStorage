using Microsoft.AspNetCore.Components;
using SNEStorage.Components.Shared;

namespace SNEStorage.Components.Pages;

[RequireAccount(ResourceManagement = true)]
public partial class ResourceEdit
{
    [Parameter] public long ResourceId { get; set; }
}
