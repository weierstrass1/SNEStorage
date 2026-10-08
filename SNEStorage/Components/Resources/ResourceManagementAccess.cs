using SNEStorage.Models;

namespace SNEStorage.Components.Resources;

// Controls presentation of management links. The API enforces authorization.
internal static class ResourceManagementAccess
{
    public static bool CanDisplay(Resource? resource, string? userId) =>
        resource is not null && !string.IsNullOrWhiteSpace(userId) &&
        (resource.SubmitterId == userId ||
         resource.ResourcesAuthors.Any(author => author.UserId == userId) ||
         resource.ResourceVersions.Any(version => version.SubmitterUserId == userId));
}
