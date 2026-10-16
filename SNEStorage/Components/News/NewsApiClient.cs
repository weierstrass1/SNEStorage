using System.Text.Json;
using Microsoft.JSInterop;
using SNEStorage.DTOs;
using SNEStorage.Services;

namespace SNEStorage.Components.News;

// Browser HTTP requests to NewsController; no business logic or database access.
internal sealed class NewsApiClient(IJSObjectReference module, string requestKey)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public Task<NewsWriteResponse> CreateAsync(NewsCreateDTO dto, CancellationToken ct) =>
        module.InvokeAsync<NewsWriteResponse>("createNews", ct, requestKey, dto).AsTask();

    public Task<NewsWriteResponse> EditAsync(long newsId, NewsEditDTO dto, CancellationToken ct) =>
        module.InvokeAsync<NewsWriteResponse>("editNews", ct, requestKey, newsId, dto).AsTask();

    public Task<NewsWriteResponse> VoteAsync(long newsId, LikeValues value, string userId, CancellationToken ct) =>
        module.InvokeAsync<NewsWriteResponse>("submitNewsVote", ct, $"{requestKey}-vote", newsId, value.ToString(), userId).AsTask();

    public async Task<NewsReadResponse> ReadOneAsync(long newsId, CancellationToken ct)
    {
        var json = await module.InvokeAsync<string>("readNewsItem", ct, requestKey, newsId);
        var response = JsonSerializer.Deserialize<BrowserResponse>(json, JsonOptions)
            ?? throw new JsonException("The news endpoint returned no response.");
        if (response.Status is < 200 or >= 300) return new(response.Status, null, response.Body);
        var item = JsonSerializer.Deserialize<NewsReadDTO>(response.Body, JsonOptions);
        if (item is null || item.Id != newsId) throw new JsonException("The news endpoint returned an unexpected announcement.");
        return new(response.Status, item, response.Body);
    }

    public async Task<List<NewsReadDTO>> ReadAsync(int amount, int page, NewsSorting sort)
    {
        var json = await module.InvokeAsync<string>("readNews", requestKey, amount, page, sort.ToString());
        var response = JsonSerializer.Deserialize<BrowserResponse>(json, JsonOptions);
        if (response is null || response.Status is < 200 or >= 300)
            throw new JsonException("The news endpoint did not return a successful response.");

        return JsonSerializer.Deserialize<List<NewsReadDTO>>(response.Body, JsonOptions)
            ?? throw new JsonException("The news endpoint returned no list.");
    }

    private sealed record BrowserResponse(int Status, string Body);
}

internal sealed record NewsWriteResponse(int Status, string Body);
internal sealed record NewsReadResponse(int Status, NewsReadDTO? Item, string Body);
