using SNEStorage.DTOs;

namespace SNEStorage.Components.Resources;

// Controls presentation of management links. The API enforces authorization.
internal static class ResourceManagementAccess
{
    public static bool CanDisplay(ResourceReadDTO? resource, string? userId, bool staffResourceEdit = false) =>
        !string.IsNullOrWhiteSpace(userId) && resource is not null &&
        (resource.CanEdit || staffResourceEdit);
}
