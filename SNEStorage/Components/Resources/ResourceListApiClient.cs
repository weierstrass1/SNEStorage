using System.Text.Json;
using Microsoft.JSInterop;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Resources;

internal sealed class ResourceListApiClient(IJSObjectReference module, string requestKey)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ResourceReadResult<List<ResourceFinderViewDTO>>> FindAsync(
        ResourceListFilterState filter, int amount, int page) =>
        Decode<List<ResourceFinderViewDTO>>(await module.InvokeAsync<string>(
            "findResources", requestKey, filter.Finder, filter.AuthorLike, amount, page));

    public async Task<ResourceReadResult<ResourceCreateOptionsDto>> OptionsAsync() =>
        Decode<ResourceCreateOptionsDto>(await module.InvokeAsync<string>("getOptions", requestKey));

    public async Task<ResourceReadResult<List<ResourceFinderViewDTO>>> FindLatestPublicAsync() =>
        Decode<List<ResourceFinderViewDTO>>(await module.InvokeAsync<string>(
            "findPublicResources", requestKey, new ResourceFinderDTO
            {
                CanIncludesPorn = false,
                CanIncludesGore = false,
                CanIncludesPolitics = false,
                CanIncludesSlurs = false,
                CanIncludesSensitiveContent = false,
                Sorting = new() { SortType = ResourceSorting.CreatedAt, SortOrderDescending = true }
            }, 5, 0));

    private static ResourceReadResult<T> Decode<T>(string json) where T : class
    {
        var response = JsonSerializer.Deserialize<BrowserResponse>(json, JsonOptions);
        if (response is null) return new(502, null, null);
        if (response.Cancelled) return new(499, response.Body, null);
        if (response.Status is < 200 or >= 300) return new(response.Status, response.Body, null);
        var data = JsonSerializer.Deserialize<T>(response.Body ?? "null", JsonOptions);
        return new(data is null ? 502 : response.Status, response.Body, data);
    }
    private sealed record BrowserResponse(int Status, string? Body, bool Cancelled);
}
