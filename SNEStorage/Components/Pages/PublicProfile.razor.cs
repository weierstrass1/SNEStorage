using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Pages;

public partial class PublicProfile
{
    [Parameter] public string UserId { get; set; } = string.Empty;
}