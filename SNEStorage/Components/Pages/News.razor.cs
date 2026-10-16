using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using SNEStorage.Components.Shared;
using SNEStorage.Services;

namespace SNEStorage.Components.Pages;

public partial class News : IAsyncDisposable
{
    private bool _canCreateNews, _disposed;
    private NewsSorting _sort = NewsSorting.RecentFirst;
    private IJSObjectReference? _module, _monitor;
    private DotNetObjectReference<News>? _receiver;

    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private AuthenticationStateProvider Authentication { get; set; } = default!;
    [Inject] private ILogger<News> Logger { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed) return;
        try
        {
            var module = await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/account-navigation.js?v=10");
            if (_disposed) { await module.DisposeAsync(); return; }
            _module = module;
            _receiver = DotNetObjectReference.Create(this);
            _monitor = await _module.InvokeAsync<IJSObjectReference>("watchAccount", _receiver);
            if (_disposed) await StopMonitorAsync();
        }
        catch (JSDisconnectedException) { }
        catch (JSException exception)
        {
            Logger.LogDebug(exception, "News creation access could not be verified.");
        }
    }

    [JSInvokable]
    public Task UpdateAccount(BrowserAccount account) => InvokeAsync(async () =>
    {
        if (_disposed) return;
        // Hide on logout, uncertainty or an account change; never use circuit claims alone.
        if (account.Authenticated != true || string.IsNullOrWhiteSpace(account.UserId))
        {
            _canCreateNews = false;
            StateHasChanged();
            return;
        }
        var state = await Authentication.GetAuthenticationStateAsync();
        if (_disposed) return;
        _canCreateNews = StaffAccess.IsAllowed(state.User, account.UserId);
        StateHasChanged();
    });

    private async Task StopMonitorAsync()
    {
        if (_monitor is null) return;
        await _monitor.InvokeVoidAsync("stop");
        await _monitor.DisposeAsync();
        _monitor = null;
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        try
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            if (_monitor is not null)
            {
                await _monitor.InvokeVoidAsync("stop", cleanup.Token);
                await _monitor.DisposeAsync();
            }
            if (_module is not null) await _module.DisposeAsync();
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
        finally { _receiver?.Dispose(); }
    }

    public sealed class BrowserAccount
    {
        public bool? Authenticated { get; set; }
        public string? UserId { get; set; }
    }
}
