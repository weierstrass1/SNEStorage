using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Pages;

public partial class Login
{
    [Parameter, SupplyParameterFromQuery(Name = "returnUrl")]
    public string? ReturnUrl { get; set; }

    protected string LoginReturnUrl => LoginNavigation.Normalize(ReturnUrl);
    protected string LoginAction => "/api/login/submit?returnUrl=" + Uri.EscapeDataString(LoginReturnUrl);

    [Parameter, SupplyParameterFromQuery(Name = "registered")]
    public string? Registered { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "result")]
    public string? Result { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "emailConfirmed")]
    public string? EmailConfirmed { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "passwordChanged")]
    public string? PasswordChanged { get; set; }

    protected string? StatusMessage => PasswordChanged == "1"
        ? "Your password has been updated. Log in with your new password."
        : EmailConfirmed == "1"
        ? "Your email has been confirmed. You can now log in."
        : Registered == "1"
        ? "Your account was created. Check your inbox to confirm your email before logging in."
        : Result switch
        {
            null or "" => null,
            "invalid" or "InvalidPassword" or "UserNotFound" =>
                "Login failed. Check your username and password.",
            "rate-limited" =>
                "Too many login attempts. Please wait a moment before trying again.",
            "AccountLockedOut" =>
                "Your account is temporarily locked after too many failed login attempts. Try again in 10 minutes.",
            "EmailNotConfirmed" =>
                "Confirm your email before logging in. Check your inbox or use the confirmation email link below.",
            "DBError" or "CacheError" or "TokenCreationError" or "InvalidToken" or
            "Unavailable" or "InvalidConfiguration" or "Error" =>
                "Login is temporarily unavailable. Please try again later.",
            "CancelledToken" or "CancellationTokenError" =>
                "Login could not finish. Please try again.",
            _ => "Login could not be completed. Please try again."
        };
}
