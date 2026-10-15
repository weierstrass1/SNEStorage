using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace SNEStorage.Components.Pages;

public partial class ApplyPatches : IAsyncDisposable
{
    private readonly string _patcherId = "rom-patcher-" + Guid.NewGuid().ToString("N");
    private IJSObjectReference? _module;
    private bool _disposed;
    private string? _initializationError;
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed) return;
        try
        {
            var module = await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/rom-patcher.js?v=1");
            if (_disposed) { await module.DisposeAsync(); return; }
            _module = module;
            await module.InvokeVoidAsync("initializePatcher", _patcherId);
        }
        catch (JSDisconnectedException) { }
        catch (JSException)
        {
            if (_disposed) return;
            _initializationError = "The patcher could not start. Reload the page or try another browser.";
            StateHasChanged();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (_module is null) return;
        try
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await _module.InvokeVoidAsync("disposePatcher", cleanup.Token, _patcherId);
            await _module.DisposeAsync();
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
    }
}
