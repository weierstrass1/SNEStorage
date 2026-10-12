using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace SNEStorage.Components.Pages;

[AllowAnonymous]
public partial class CheckEmail : IAsyncDisposable
{
    private bool _checkingSession = true;
    private bool _isAuthenticated;
    private bool _disposed;
    private IJSObjectReference? _sessionModule;
    private IJSObjectReference? _sessionMonitor;

    [Parameter, SupplyParameterFromQuery(Name = "delivery")]
    public string? Delivery { get; set; }

    protected bool EmailDeliveryFailed => Delivery == "failed";

    [CascadingParameter]
    private Task<AuthenticationState> AuthenticationStateTask { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private IJSRuntime JavaScript { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        var authenticationState = await AuthenticationStateTask;
        _isAuthenticated = authenticationState.User.Identity?.IsAuthenticated == true;
        _checkingSession = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_isAuthenticated)
        {
            Navigation.NavigateTo("/", forceLoad: true, replace: true);
            return;
        }

        if (firstRender && !_disposed)
        {
            _sessionModule = await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/check-email.js?v=3");
            if (_disposed)
            {
                await _sessionModule.DisposeAsync();
                return;
            }

            _sessionMonitor = await _sessionModule.InvokeAsync<IJSObjectReference>(
                "watchSession", Navigation.BaseUri);
            if (_disposed)
            {
                await StopMonitorAsync();
            }
        }
    }

    private async ValueTask StopMonitorAsync()
    {
        if (_sessionMonitor is not null)
        {
            await _sessionMonitor.InvokeVoidAsync("stop");
            await _sessionMonitor.DisposeAsync();
            _sessionMonitor = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        try
        {
            await StopMonitorAsync();
            if (_sessionModule is not null)
            {
                await _sessionModule.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // The browser connection has already closed.
        }
    }
}
