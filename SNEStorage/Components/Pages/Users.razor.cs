using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace SNEStorage.Components.Pages;

public partial class Users : IAsyncDisposable
{
    private readonly string _directoryId = "users-" + Guid.NewGuid().ToString("N");
    private IJSObjectReference? _module;
    private bool _disposed;
    private string? _initializationError;
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed) return;
        try
        {
            var module = await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/users.js?v=3");
            if (_disposed) { await module.DisposeAsync(); return; }
            _module = module;
            await module.InvokeVoidAsync("initialize", _directoryId);
        }
        catch (JSDisconnectedException) { }
        catch (JSException)
        {
            if (_disposed) return;
            _initializationError = "User search could not start. Please reload the page.";
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
            await _module.InvokeVoidAsync("dispose", cleanup.Token, _directoryId);
            await _module.DisposeAsync();
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException or ObjectDisposedException) { }
    }
}
