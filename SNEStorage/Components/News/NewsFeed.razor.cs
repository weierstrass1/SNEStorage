using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SNEStorage.DTOs;
using SNEStorage.Services;
using SNEStorage.Components.Shared;

namespace SNEStorage.Components.News;

public partial class NewsFeed : IAsyncDisposable
{
    private const int PageSize = 10;
    private readonly string _requestKey = Guid.NewGuid().ToString("N");
    private readonly List<NewsReadDTO> _items = [];
    private IJSObjectReference? _module;
    private IJSObjectReference? _accountMonitor;
    private NewsFeedContext? _reactionsContext;
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
            var initial = await BrowserReadStartup.LoadAsync<string>(JavaScript,
                "./js/news.js?v=9", "readNews", [_requestKey, PageSize, 0, Sort.ToString()], _requestKey, "dispose");
            if (_disposed) { if (initial.Module is not null) await initial.Module.DisposeAsync(); return; }
            if (initial.Cancelled || initial.Module is null || initial.Response is null) return;
            var module = _module = initial.Module;
            _api = new(module, _requestKey);
            _reactionsContext = new(module, null);
            var items = NewsApiClient.DecodeList(initial.Response);
            _items.AddRange(items);
            _page = 1;
            _endReached = !Infinite || items.Count < PageSize;
            _initializing = false;
            _syncObserver = true;
            _focusPending = Infinite;
            _reference ??= DotNetObjectReference.Create(this);
            StateHasChanged();
            try
            {
                var monitor = await module.InvokeAsync<IJSObjectReference>("watchAccount", _reference);
                if (_disposed) { await monitor.InvokeVoidAsync("stop"); await monitor.DisposeAsync(); }
                else _accountMonitor = monitor;
            }
            catch (JSException exception) { if (!_disposed) Logger.LogDebug(exception, "News account monitoring could not be initialized."); }
        }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception) when (exception is JSException or JsonException)
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
    public Task UpdateAccount(VerifiedAccountContent.BrowserAccount account) => InvokeAsync(() =>
    {
        if (_disposed || _module is null || account.Authenticated is null) return;
        _reactionsContext = new(_module, account);
        StateHasChanged();
    });
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
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await BrowserReadStartup.CancelAsync(JavaScript, _requestKey, cleanup.Token);
            if (_module is not null)
            {
                if (_accountMonitor is not null)
                {
                    await _accountMonitor.InvokeVoidAsync("stop", cleanup.Token);
                    await _accountMonitor.DisposeAsync();
                }
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
