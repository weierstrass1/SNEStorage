using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Pages;

public partial class Login
{
    [Parameter, SupplyParameterFromQuery(Name = "returnUrl")]
    public string? ReturnUrl { get; set; }

    protected string LoginReturnUrl => LoginNavigation.Normalize(ReturnUrl);

    [Parameter, SupplyParameterFromQuery(Name = "registered")]
    public string? Registered { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "result")]
    public string? Result { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "emailConfirmed")]
    public string? EmailConfirmed { get; set; }

    protected string? StatusMessage => EmailConfirmed == "1"
        ? "Your email has been confirmed. You can now log in."
        : Registered == "1"
        ? "Your account was created. Check your inbox to confirm your email before logging in."
        : Result == "invalid"
            ? "Login failed. Check your username and password."
            : null;
}
