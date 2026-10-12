using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace SNEStorage.Components.Shared;

// Browser loading and cleanup shared by public pages backed by static JSON.
public abstract class StaticDocumentPage<TDocument> : ComponentBase, IAsyncDisposable where TDocument : class
{
    private readonly string _requestKey = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _lifetime = new();
    private IJSObjectReference? _module;
    private bool _disposed;
    private bool _scrollToAnchor;
    protected TDocument? _document;
    protected string? _error;
    protected bool _loading = true;

    protected abstract string DocumentPath { get; }
    protected abstract string LoadErrorMessage { get; }
    protected virtual void OnDocumentLoaded(TDocument document) { }

    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<StaticDocumentPage<TDocument>> Logger { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await LoadAsync();
        else if (_scrollToAnchor && _module is not null && !_disposed)
        {
            _scrollToAnchor = false;
            try { await _module.InvokeVoidAsync("scrollToAnchor", _lifetime.Token); }
            catch (JSDisconnectedException) { }
            catch (OperationCanceledException) when (_disposed) { }
        }
    }

    protected async Task LoadAsync()
    {
        if (_disposed) return;
        _loading = true;
        _error = null;
        try
        {
            _module ??= await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/site-rules.js?v=3");
            if (_disposed)
            {
                await _module.DisposeAsync();
                return;
            }

            var document = await _module.InvokeAsync<TDocument>(
                "readDocument", _lifetime.Token, Navigation.BaseUri, _requestKey, DocumentPath);
            if (!_disposed)
            {
                OnDocumentLoaded(document);
                _document = document;
                _scrollToAnchor = true;
            }
        }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception)
        {
            if (!_disposed)
            {
                Logger.LogWarning(exception, "The public document {DocumentPath} could not be loaded.", DocumentPath);
                _error = LoadErrorMessage;
            }
        }
        finally
        {
            if (!_disposed)
            {
                _loading = false;
                await InvokeAsync(StateHasChanged);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _lifetime.Cancel();
        if (_module is not null)
        {
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _module.InvokeVoidAsync("cancelRead", cleanup.Token, _requestKey);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException) { }
            catch (OperationCanceledException) { }
        }
        _lifetime.Dispose();
    }
}
