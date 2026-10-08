using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace SNEStorage.Components.Resources;

public partial class ResourceCommentList
{
    private readonly CancellationTokenSource _feedLifetime = new();
    private ElementReference _feedElement;
    private ElementReference _sentinelElement;
    private IJSObjectReference? _feedModule;
    private IJSObjectReference? _feedObserver;
    private DotNetObjectReference<ResourceCommentList>? _feedReceiver;
    private long? _scrollTarget;
    private bool _feedUnavailable;
    private bool _feedUpdating;
    private bool _feedDirty;

    private async Task UpdateFeedAsync()
    {
        if (_disposed || _feedUnavailable) return;
        if (_feedUpdating) { _feedDirty = true; return; }
        _feedUpdating = true;
        _feedDirty = false;
        try
        {
            _feedModule ??= await JavaScript.InvokeAsync<IJSObjectReference>("import", _feedLifetime.Token, "./js/comment-feed.js?v=1");
            if (_disposed) return;
            if (_feedObserver is null)
            {
                _feedReceiver = DotNetObjectReference.Create(this);
                _feedObserver = await _feedModule.InvokeAsync<IJSObjectReference>("createCommentFeed", _feedLifetime.Token,
                    _feedElement, _sentinelElement, _feedReceiver);
            }
            if (_disposed) return;
            if (_scrollTarget is { } id)
            {
                _scrollTarget = null;
                await _feedModule.InvokeVoidAsync("revealComment", _feedLifetime.Token, _feedElement, id.ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
            await _feedObserver.InvokeVoidAsync("update", _feedLifetime.Token,
                _loaded && _hasNext && !_loading && !_busy && !_pending && _error is null, _generation);
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException)
        {
            // Loading manually remains available if the browser observer cannot run.
            _feedUnavailable = true;
            Logger.LogDebug(exception, "Automatic comment loading was interrupted or unavailable.");
        }
        finally
        {
            _feedUpdating = false;
            if (!_disposed && !_feedUnavailable && _feedDirty) StateHasChanged();
        }
    }

    [JSInvokable]
    public Task LoadMoreComments(int generation) => !_disposed && generation == _generation && _error is null
        ? InvokeAsync(LoadMoreAsync) : Task.CompletedTask;

    private async ValueTask DisposeFeedAsync()
    {
        _feedLifetime.Cancel();
        try
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            if (_feedObserver is not null)
            {
                await _feedObserver.InvokeVoidAsync("stop", cleanup.Token);
                await _feedObserver.DisposeAsync();
            }
            if (_feedModule is not null) await _feedModule.DisposeAsync();
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
        finally
        {
            _feedReceiver?.Dispose();
            _feedLifetime.Dispose();
        }
    }
}
