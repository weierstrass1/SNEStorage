using System.Text.Json;
using SNEStorage.Services;

namespace SNEStorage.Components.Resources;

// Presentation/response parsing only. Persistence and authorization belong to the controller.
internal static class ResourceEditorResponse
{
    public static bool ConfirmsSave(string? body, ResourceEditorMode mode, long resourceId)
    {
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
            return value.ValueKind is JsonValueKind.String or JsonValueKind.Number
                && Enum.TryParse<ResourceServiceResult>(value.ToString(), true, out var result)
                && result == ResourceServiceResult.Success;
        }
        catch (JsonException) { return false; }
    }

    public static string[] ValidationErrors(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return [];
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object
                || !document.RootElement.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Object) return [];
            return errors.EnumerateObject().Where(field => field.Value.ValueKind == JsonValueKind.Array)
                .SelectMany(field => field.Value.EnumerateArray())
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString()!).ToArray();
        }
        catch (JsonException) { return []; }
    }
}
