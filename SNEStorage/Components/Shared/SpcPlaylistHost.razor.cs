using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace SNEStorage.Components.Shared;

public partial class SpcPlaylistHost : IAsyncDisposable
{
    private readonly string _dockId = "spc-dock-" + Guid.NewGuid().ToString("N");
    private IJSObjectReference? _module;
    private bool _disposed;
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private ILogger<SpcPlaylistHost> Logger { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed) return;
        try
        {
            var module = await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/spc-playlist.js?v=5");
            if (_disposed) { await module.DisposeAsync(); return; }
            _module = module;
            await module.InvokeVoidAsync("mountDock", _dockId);
            if (_disposed) await module.InvokeVoidAsync("unmount", _dockId);
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException)
        {
            Logger.LogDebug(exception, "The floating SPC player could not be initialized.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (_module is null) return;
        try
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await _module.InvokeVoidAsync("unmount", cleanup.Token, _dockId);
            await _module.DisposeAsync();
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
    }
}
