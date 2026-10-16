using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace SNEStorage.Components.Shared;

public partial class VerifiedAccountContent : IAsyncDisposable
{
    private bool _authenticated, _disposed;
    private IJSObjectReference? _module, _monitor;
    private DotNetObjectReference<VerifiedAccountContent>? _receiver;
    private int _revision;

    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public bool StaffOnly { get; set; }
    [Parameter] public EventCallback<bool> AccessChanged { get; set; }
    [Inject] private AuthenticationStateProvider Authentication { get; set; } = default!;
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private ILogger<VerifiedAccountContent> Logger { get; set; } = default!;

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
            if (_disposed && _monitor is not null)
            {
                await _monitor.InvokeVoidAsync("stop");
                await _monitor.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { }
        catch (JSException exception)
        {
            Logger.LogDebug(exception, "Account controls could not be verified.");
        }
    }

    [JSInvokable]
    public Task UpdateAccount(BrowserAccount account) => InvokeAsync(async () =>
    {
        if (_disposed) return;
        var revision = ++_revision;
        // An unavailable API does not prove that a previously verified session ended.
        if (account.Authenticated is null) return;
        var allowed = account.Authenticated == true && !string.IsNullOrWhiteSpace(account.UserId);
        if (allowed && StaffOnly)
        {
            var state = await Authentication.GetAuthenticationStateAsync();
            if (_disposed || revision != _revision) return;
            allowed = StaffAccess.IsAllowed(state.User, account.UserId);
        }
        if (_authenticated == allowed) return;
        _authenticated = allowed;
        await AccessChanged.InvokeAsync(allowed);
        if (_disposed) return;
        StateHasChanged();
    });

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
