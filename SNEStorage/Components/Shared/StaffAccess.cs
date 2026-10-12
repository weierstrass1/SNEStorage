using System.Security.Claims;

namespace SNEStorage.Components.Shared;

// UI access checks mirror the existing claims; the API enforces authorization.
internal static class StaffAccess
{
    public static bool IsAllowed(ClaimsPrincipal user, string? verifiedUserId = null) =>
        user.Identity?.IsAuthenticated == true &&
        (user.HasClaim("Admin", "1") || user.HasClaim("Moderator", "1")) &&
        (verifiedUserId is null || user.FindFirstValue(ClaimTypes.NameIdentifier) == verifiedUserId);
}
