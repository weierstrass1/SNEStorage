using System.Text.Json;
using Microsoft.JSInterop;

namespace SNEStorage.Components.Resources;

// Browser HTTP transport to ResourceController; no services or database access.
internal sealed class ResourceApiClient(IJSObjectReference module, string requestKey, CancellationToken cancellationToken)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ResourceReadResult<T>> ReadSectionAsync<T>(long resourceId, string section) where T : class
    {
        var json = await module.InvokeAsync<string>("getResourceSection", cancellationToken, resourceId, section, requestKey);
        return Decode<T>(json);
    }

    internal static ResourceReadResult<T> Decode<T>(string json) where T : class
    {
        var response = JsonSerializer.Deserialize<BrowserResponse>(json, JsonOptions);
        if (response is null) return new(502, null, null);
        if (response.Cancelled) return new(0, response.Body, null);
        if (response.Status is < 200 or >= 300) return new(response.Status, response.Body, null);
        if (string.IsNullOrWhiteSpace(response.Body)) return new(502, response.Body, null);
        var data = JsonSerializer.Deserialize<T>(response.Body, JsonOptions);
        return new(data is null ? 502 : response.Status, response.Body, data);
    }

    private sealed class BrowserResponse
    {
        public int Status { get; set; }
        public string? Body { get; set; }
        public bool Cancelled { get; set; }
    }
}

internal sealed record ResourceReadResult<T>(int Status, string? Body, T? Data) where T : class
{
    public bool Success => Status is >= 200 and < 300 && Data is not null;
}
