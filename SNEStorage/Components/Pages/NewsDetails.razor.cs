using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SNEStorage.Components.News;
using SNEStorage.DTOs;
using SNEStorage.Components.Shared;

namespace SNEStorage.Components.Pages;

public partial class NewsDetails : IAsyncDisposable
{
    private readonly string _requestKey = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _lifetime = new();
    private IJSObjectReference? _module;
    private NewsReadDTO? _news;
    private long? _previousId;
    private int? _errorStatus;
    private string? _errorBody;
    private int _generation;
    private bool _loading = true, _pending, _canEdit, _disposed;

    [Parameter] public long NewsId { get; set; }
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private ILogger<NewsDetails> Logger { get; set; } = default!;

    protected override void OnParametersSet()
    {
        if (_previousId == NewsId) return;
        _previousId = NewsId;
        Reload();
    }

    private void Reload()
    {
        _generation++;
        _news = null;
        _errorStatus = null;
        _errorBody = null;
        _loading = _pending = true;
    }

    private void StaffAccessChanged(bool allowed) => _canEdit = allowed;
    private void ArticleUpdated(NewsReadDTO article) => _news = article;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_pending || _disposed) return;
        _pending = false;
        var generation = _generation;
        var newsId = NewsId;
        try
        {
            if (newsId <= 0) { _errorStatus = 404; return; }
            NewsReadResponse response;
            if (_module is null)
            {
                var initial = await BrowserReadStartup.LoadAsync<string>(JavaScript,
                    "./js/news.js?v=9", "readNewsItem", [_requestKey, newsId], _requestKey, "dispose");
                if (_disposed || generation != _generation)
                {
                    if (initial.Module is not null) await initial.Module.DisposeAsync();
                    return;
                }
                if (initial.Cancelled || initial.Module is null || initial.Response is null) return;
                _module = initial.Module;
                response = NewsApiClient.DecodeOne(initial.Response, newsId);
            }
            else response = await new NewsApiClient(_module, _requestKey).ReadOneAsync(newsId, _lifetime.Token);
            if (_disposed || generation != _generation) return;
            _news = response.Item;
            _errorStatus = response.Item is null ? response.Status : null;
            _errorBody = response.Item is null ? response.Body : null;
        }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception) when (exception is JSException or JsonException or OperationCanceledException)
        {
            if (_disposed || generation != _generation) return;
            Logger.LogWarning(exception, "News {NewsId} could not be loaded.", newsId);
            _errorStatus = exception is JsonException ? 502 : 0;
        }
        finally
        {
            if (!_disposed && generation == _generation)
            {
                _loading = false;
                StateHasChanged();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _generation++;
        _lifetime.Cancel();
        try
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await BrowserReadStartup.CancelAsync(JavaScript, _requestKey, cleanup.Token);
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
        if (_module is not null)
        {
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _module.InvokeVoidAsync("dispose", cleanup.Token, _requestKey);
                await _module.DisposeAsync();
            }
            catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
        }
        _lifetime.Dispose();
    }
}
