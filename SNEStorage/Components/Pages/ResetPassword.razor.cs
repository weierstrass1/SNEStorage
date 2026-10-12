using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Pages;

[AllowAnonymous]
public partial class ResetPassword
{
    [Parameter] public string? UserId { get; set; }
    [Parameter, SupplyParameterFromQuery(Name = "token")]
    public string? Token { get; set; }
}
