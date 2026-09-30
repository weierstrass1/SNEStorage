using System.Globalization;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SNEStorage.Models;

namespace SNEStorage.Services;

public sealed class ResourceAccessService(SnestorageContext context)
{
    public bool IsStaff(ClaimsPrincipal user) =>
        HasClaimOrRole(user, "Admin") || HasClaimOrRole(user, "Moderator");

    public bool CanUpload(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;

    public static bool IsPublic(string? visibilityName) =>
        Normalize(visibilityName) is "public" or "publico" or "publica";

    public static bool IsUnlisted(string? visibilityName) =>
        Normalize(visibilityName) is "unlisted" or "no listado" or "no listada" or "no-listado";

    public static bool IsPrivate(string? visibilityName) =>
        Normalize(visibilityName) is "private" or "privado" or "privada";

    public async Task<bool> CanListAsync(Resource resource, ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        if (IsStaff(user))
            return true;

        var visibilityName = resource.Visibility?.Name ?? await context.Visibilities
            .Where(item => item.Id == resource.VisibilityId)
            .Select(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken);

        return IsPublic(visibilityName);
    }

    public async Task<bool> CanViewAsync(
        Resource resource,
        ClaimsPrincipal user,
        bool directLink,
        CancellationToken cancellationToken = default)
    {
        if (IsStaff(user))
            return true;

        var visibilityName = resource.Visibility?.Name ?? await context.Visibilities
            .Where(item => item.Id == resource.VisibilityId)
            .Select(item => item.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (IsPublic(visibilityName))
            return true;

        if (IsUnlisted(visibilityName))
            return directLink;

        if (!IsPrivate(visibilityName))
            return false;

        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
            return false;

        return resource.SubmitterUserId == userId || await context.ResourcesAuthors
            .AnyAsync(item => item.ResourceId == resource.Id && item.UserId == userId, cancellationToken);
    }

    private static bool HasClaimOrRole(ClaimsPrincipal user, string name) =>
        user.IsInRole(name) || user.Claims.Any(claim =>
            string.Equals(claim.Type, name, StringComparison.OrdinalIgnoreCase) ||
            (string.Equals(claim.Type, ClaimTypes.Role, StringComparison.OrdinalIgnoreCase) &&
             string.Equals(claim.Value, name, StringComparison.OrdinalIgnoreCase)));

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var normalized = value.Trim().Normalize(NormalizationForm.FormD);
        var result = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                result.Append(char.ToLowerInvariant(character));
        }

        return result.ToString().Normalize(NormalizationForm.FormC);
    }
}
