using Microsoft.AspNetCore.Components;
using SNEStorage.Components.Shared;

namespace SNEStorage.Components.Pages;

[RequireAccount]
public partial class Profile
{
    [CascadingParameter(Name = "VerifiedPageAccount")]
    private AccountPageContext? PageAccount { get; set; }
}