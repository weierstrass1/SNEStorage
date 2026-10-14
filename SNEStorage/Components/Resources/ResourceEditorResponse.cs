using System.Text.Json;

namespace SNEStorage.Components.Resources;

// Presentation/response parsing only. Persistence and authorization belong to the controller.
internal static class ResourceEditorResponse
{
    public static bool ConfirmsSave(int status, string? body, ResourceEditorMode mode, long resourceId)
    {
        if (status == 204 && mode != ResourceEditorMode.CreateVersion) return true;
        if (string.IsNullOrWhiteSpace(body)) return false;
        try
        {
            using var document = JsonDocument.Parse(body);
            var value = document.RootElement;
            if (mode == ResourceEditorMode.CreateVersion)
            {
                return value.ValueKind == JsonValueKind.Object
                    && value.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var versionId) && versionId > 0
                    && value.TryGetProperty("resourceId", out var resource) && resource.ValueKind == JsonValueKind.Number && resource.TryGetInt64(out var parentId) && parentId == resourceId;
            }
            return ApiErrorPresentation.IsSuccess(body, "resource");
        }
        catch (JsonException) { return false; }
    }

    public static string[] ValidationErrors(string? body)
        => ApiErrorPresentation.ValidationErrors(body);
}
