using Microsoft.AspNetCore.Components;
using SNEStorage.Components.Shared;

namespace SNEStorage.Components.Pages;

[RequireAccount(ResourceManagement = true)]
public partial class ResourceVersionCreate
{
    [Parameter] public long ResourceId { get; set; }
}
