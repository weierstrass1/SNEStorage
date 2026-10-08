using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.WebUtilities;

namespace SNEStorage.Components;

// Presentation navigation only: carry the page being viewed through the login form.
internal static class LoginNavigation
{
    public static string GetLoginUrl(NavigationManager navigation)
    {
        var current = navigation.ToAbsoluteUri(navigation.Uri);
        var destination = IsAccountEntry(current.AbsolutePath)
            ? Normalize(QueryHelpers.ParseQuery(current.Query).GetValueOrDefault("returnUrl").ToString())
            : Normalize(current.PathAndQuery + current.Fragment);
        return "/Login?returnUrl=" + Uri.EscapeDataString(destination);
    }

    public static string Normalize(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl) || !RedirectHttpResult.IsLocalUrl(returnUrl)) return "/";
        var destination = returnUrl.StartsWith("~/", StringComparison.Ordinal) ? returnUrl[1..] : returnUrl;
        var path = destination.Split('?', '#')[0];
        return IsAccountEntry(path.TrimEnd('/')) ? "/" : destination;
    }

    private static bool IsAccountEntry(string path) =>
        path.Equals("/Login", StringComparison.OrdinalIgnoreCase) ||
        path.Equals("/Register", StringComparison.OrdinalIgnoreCase) ||
        path.StartsWith("/Register/", StringComparison.OrdinalIgnoreCase);
}
