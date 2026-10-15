using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SNEStorage.DTOs;
using SNEStorage.Services;

namespace SNEStorage.Components.News;

public partial class NewsFeed : IAsyncDisposable
{
    private const int PageSize = 10;
    private readonly string _requestKey = Guid.NewGuid().ToString("N");
    private readonly List<NewsReadDTO> _items = [];
    private IJSObjectReference? _module;
    private DotNetObjectReference<NewsFeed>? _reference;
    private NewsApiClient? _api;
    private ElementReference _sentinel;
    private int _page;
    private bool _initializing = true, _loading, _endReached, _syncObserver, _focusPending, _disposed;
    private string? _error;
    [Parameter] public bool Compact { get; set; }
    [Parameter] public bool Infinite { get; set; }
    [Parameter] public bool CanEdit { get; set; }
    [Parameter] public NewsSorting Sort { get; set; } = NewsSorting.RecentFirst;
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private ILogger<NewsFeed> Logger { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed) return;
        if (firstRender)
        {
            await InitializeAsync();
            return;
        }
        if (_module is null || !_syncObserver) return;
        _syncObserver = false;
        try
        {
            await _module.InvokeVoidAsync("observe", _requestKey, _sentinel, _reference,
                Infinite && !_loading && !_endReached && _error is null);
            if (_focusPending)
            {
                _focusPending = false;
                await _module.InvokeVoidAsync("focusNews");
            }
        }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) when (_disposed) { }
        catch (JSException exception) { Logger.LogWarning(exception, "News scrolling could not be initialized."); }
    }
    private async Task InitializeAsync()
    {
        if (_disposed) return;
        _initializing = true;
        _error = null;
        try
        {
            var module = await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/news.js?v=6");
            if (_disposed) { await module.DisposeAsync(); return; }
            _module = module;
            _api = new(module, _requestKey);
            if (Infinite) _reference = DotNetObjectReference.Create(this);
            await LoadMoreAsync();
        }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) when (_disposed) { }
        catch (JSException exception)
        {
            Logger.LogWarning(exception, "The news feed could not be initialized.");
            _error = "News could not be loaded. Please try again.";
        }
        finally
        {
            if (!_disposed) { _initializing = false; await InvokeAsync(StateHasChanged); }
        }
    }
    [JSInvokable]
    public async Task LoadMoreAsync()
    {
        if (_disposed || _loading || _endReached || _api is null) return;
        _loading = true;
        _error = null;
        StateHasChanged();
        try
        {
            var items = await _api.ReadAsync(PageSize, _page, Sort);
            if (_disposed) return;
            var known = _items.Select(item => item.Id).ToHashSet();
            _items.AddRange(items.Where(item => known.Add(item.Id)));
            _focusPending = Infinite && _page == 0;
            _page++;
            _endReached = !Infinite || items.Count < PageSize;
        }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception) when (exception is JSException or JsonException)
        {
            if (!_disposed)
            {
                Logger.LogWarning(exception, "News could not be loaded.");
                _error = "News could not be loaded. Please try again.";
            }
        }
        finally
        {
            if (!_disposed)
            {
                _loading = false;
                _syncObserver = true;
                await InvokeAsync(StateHasChanged);
            }
        }
    }
    private Task RetryAsync() => _api is null ? InitializeAsync() : LoadMoreAsync();
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        try
        {
            if (_module is not null)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _module.InvokeVoidAsync("dispose", cleanup.Token, _requestKey);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) { }
        catch (JSException) { }
        finally { _reference?.Dispose(); }
    }
}
