using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace SNEStorage.Components.Pages;

public partial class SPCPlayer : IAsyncDisposable
{
    private readonly string _playerId = "spc-library-" + Guid.NewGuid().ToString("N");
    private IJSObjectReference? _module;
    private bool _disposed;
    private string? _initializationError;
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed) return;
        try
        {
            var module = await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/spc-playlist.js?v=4");
            if (_disposed) { await module.DisposeAsync(); return; }
            _module = module;
            await module.InvokeVoidAsync("mountPage", _playerId);
            if (_disposed) await module.InvokeVoidAsync("unmount", _playerId);
        }
        catch (JSDisconnectedException) { }
        catch (JSException)
        {
            if (_disposed) return;
            _initializationError = "The SPC player could not start. Reload the page and try again.";
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
            await _module.InvokeVoidAsync("unmount", cleanup.Token, _playerId);
            await _module.DisposeAsync();
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
    }
}
