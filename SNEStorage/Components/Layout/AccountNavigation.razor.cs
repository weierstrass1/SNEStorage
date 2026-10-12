using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace SNEStorage.Components.Layout;

public partial class AccountNavigation : IAsyncDisposable
{
    // Only the browser's current API session can resolve the initial menu state.
    private bool? _isAuthenticated;
    private string _username = "Account";
    private string? _usernameColor;
    private string? _avatarUrl;
    private bool _disposed;
    private bool _initializing;
    private int? _lastUnavailableStatus;
    private IJSObjectReference? _module;
    private IJSObjectReference? _monitor;
    private DotNetObjectReference<AccountNavigation>? _reference;
    protected string LoginUrl => LoginNavigation.GetLoginUrl(Navigation);

    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<AccountNavigation> Logger { get; set; } = default!;

    protected override void OnInitialized()
    {
        Navigation.LocationChanged += LocationChanged;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (_disposed || _initializing || _monitor is not null) return;
        _initializing = true;
        try
        {
            _module ??= await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/account-navigation.js?v=9");
            if (_disposed) { await _module.DisposeAsync(); return; }
            _reference ??= DotNetObjectReference.Create(this);
            _monitor = await _module.InvokeAsync<IJSObjectReference>("watchAccount", _reference);
            if (_disposed) await StopMonitorAsync();
        }
        catch (JSDisconnectedException) { }
        catch (JSException exception)
        {
            if (_disposed) return;
            Logger.LogWarning(exception, "Account navigation could not be initialized.");
            _lastUnavailableStatus = 0;
            StateHasChanged();
        }
        finally { _initializing = false; }
    }

    [JSInvokable]
    public Task UpdateAccount(BrowserAccount account) => InvokeAsync(() =>
    {
        if (_disposed) return;
        if (account.Authenticated == true)
        {
            _isAuthenticated = true;
            if (!string.IsNullOrWhiteSpace(account.Username)) _username = account.Username;
            _usernameColor = account.UsernameColor;
            _avatarUrl = account.AvatarUrl;
        }
        else if (account.Authenticated == false)
        {
            _isAuthenticated = false;
            _username = "Account";
            _usernameColor = _avatarUrl = null;
        }
        // An error preserves a verified state; it cannot resolve the initial unknown state.
        if (account.Authenticated is null && _lastUnavailableStatus != account.Status)
            Logger.LogWarning("Account status could not be verified: /api/profile/me returned HTTP {Status}.", account.Status);
        _lastUnavailableStatus = account.Authenticated is null ? account.Status : null;
        StateHasChanged();
    });

    private void LocationChanged(object? sender, LocationChangedEventArgs args)
    {
        if (!_disposed) _ = InvokeAsync(async () =>
        {
            if (_disposed) return;
            StateHasChanged();
            await RefreshAsync();
        });
    }

    protected async Task RefreshAsync()
    {
        if (_disposed) return;
        if (_monitor is null)
        {
            await InitializeAsync();
            return;
        }
        try { await _monitor.InvokeVoidAsync("check"); }
        catch (JSDisconnectedException) { }
        catch (JSException exception)
        {
            if (_disposed) return;
            Logger.LogWarning(exception, "Account status could not be refreshed.");
        }
    }

    private async ValueTask StopMonitorAsync()
    {
        if (_monitor is null) return;
        await _monitor.InvokeVoidAsync("stop");
        await _monitor.DisposeAsync();
        _monitor = null;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        Navigation.LocationChanged -= LocationChanged;
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
        finally { _reference?.Dispose(); }
    }

    public sealed class BrowserAccount
    {
        public bool? Authenticated { get; set; }
        public int Status { get; set; }
        public string? Username { get; set; }
        public string? UsernameColor { get; set; }
        public string? AvatarUrl { get; set; }
    }
}
