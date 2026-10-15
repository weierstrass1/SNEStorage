using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using SNEStorage.Components.Resources;
using SNEStorage.Models;
using SNEStorage.DTOs;
using RouteData = Microsoft.AspNetCore.Components.RouteData;

namespace SNEStorage.Components.Shared;

public partial class PageAccessGuard : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _requestKey = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _lifetime = new();
    private IJSObjectReference? _module;
    private IJSObjectReference? _watcher;
    private DotNetObjectReference<PageAccessGuard>? _receiver;
    private RequireAccountAttribute? _requirement;
    private AccountPageContext? _access;
    private string? _pageKey;
    private int? _errorStatus;
    private string? _errorBody;
    private string _errorDomain = "profile";
    private int _generation;
    private bool _pending;
    private bool _checking;
    private bool _accessBlocked;
    private bool _disposed;
    protected string LoginUrl => LoginNavigation.GetLoginUrl(Navigation);

    [Parameter, EditorRequired] public RouteData RouteData { get; set; } = default!;
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [CascadingParameter] private Task<AuthenticationState>? AuthenticationStateTask { get; set; }
    [CascadingParameter] private HttpContext? HttpContext { get; set; }
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<PageAccessGuard> Logger { get; set; } = default!;

    protected override async Task OnParametersSetAsync()
    {
        // An anchor scroll stays on the same page and must not recreate its form.
        var pageKey = $"{RouteData.PageType.FullName}:{new Uri(Navigation.Uri).GetLeftPart(UriPartial.Query)}";
        if (_pageKey == pageKey) return;
        _pageKey = pageKey;
        _generation++;
        _requirement = RouteData.PageType.GetCustomAttribute<RequireAccountAttribute>();
        _access = null;
        _accessBlocked = false;
        _errorStatus = null;
        _errorBody = null;
        _errorDomain = "profile";
        _pending = _requirement is not null;

        // Initial HTTP navigation can return 403; interactive navigation renders the same error panel.
        if (_requirement is not null && HttpContext is { Response.HasStarted: false } && AuthenticationStateTask is not null)
        {
            var state = await AuthenticationStateTask;
            if (state.User.Identity?.IsAuthenticated != true ||
                _requirement.StaffOnly && !StaffAccess.IsAllowed(state.User))
                HttpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || !_pending || _checking || _requirement is null) return;
        _pending = false;
        _checking = true;
        var generation = _generation;
        _errorDomain = "profile";
        var management = _requirement.ResourceManagement;
        var staffOnly = _requirement.StaffOnly;
        var resourceId = RouteData.RouteValues.GetValueOrDefault("ResourceId") as long?;
        try
        {
            _module ??= await JavaScript.InvokeAsync<IJSObjectReference>("import", _lifetime.Token, "./js/page-access.js?v=5");
            if (_disposed) return;
            if (_watcher is null)
            {
                _receiver = DotNetObjectReference.Create(this);
                _watcher = await _module.InvokeAsync<IJSObjectReference>("watchPageAccess", _lifetime.Token, _receiver);
            }
            var accountResponse = await ReadResponseAsync("getPageAccount");
            if (_disposed || generation != _generation) return;
            if (accountResponse.Status is 401 or 403) { Deny(403); return; }
            if (!AcceptResponse(accountResponse)) return;
            var profile = JsonSerializer.Deserialize<UserInfo>(accountResponse.Body, JsonOptions);
            if (string.IsNullOrWhiteSpace(profile?.UserId)) { Deny(502); return; }
            if (staffOnly)
            {
                var state = AuthenticationStateTask is null ? null : await AuthenticationStateTask;
                if (_disposed || generation != _generation) return;
                if (state is null || !StaffAccess.IsAllowed(state.User, profile.UserId)) { Deny(403); return; }
            }
            if (_access is not null && _access.Profile.UserId != profile.UserId)
            {
                _access = null;
                StateHasChanged();
            }

            ResourceReadDTO? resource = null;
            if (management)
            {
                _errorDomain = "resource";
                if (resourceId is null or <= 0) { Deny(404); return; }
                var resourceResponse = await ReadResponseAsync("getPageResource", resourceId.Value);
                if (_disposed || generation != _generation) return;
                if (resourceResponse.Status is 401 or 403) { Deny(403); return; }
                if (!AcceptResponse(resourceResponse)) return;
                resource = JsonSerializer.Deserialize<ResourceReadDTO>(resourceResponse.Body, JsonOptions);
                if (resource is null || resource.Id != resourceId) { Deny(502); return; }
                if (!ResourceManagementAccess.CanDisplay(resource, profile.UserId)) { Deny(403); return; }
            }

            // Keep the editor's current draft when a recheck confirms the same account and permissions.
            _access ??= new(profile, resource);
            _accessBlocked = false;
            _errorStatus = null;
            _errorBody = null;
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or JsonException or OperationCanceledException)
        {
            if (!_disposed && generation == _generation)
            {
                Logger.LogDebug(exception, "Page access could not be verified.");
                if (_access is null || _accessBlocked) _errorStatus = exception is JsonException ? 502 : 0;
            }
        }
        finally
        {
            _checking = false;
            if (!_disposed) StateHasChanged();
        }
    }

    private async Task<BrowserResponse> ReadResponseAsync(string method, long? resourceId = null)
    {
        var json = resourceId is null
            ? await _module!.InvokeAsync<string>(method, _lifetime.Token, _requestKey)
            : await _module!.InvokeAsync<string>(method, _lifetime.Token, resourceId.Value, _requestKey);
        return JsonSerializer.Deserialize<BrowserResponse>(json, JsonOptions) ?? new() { Status = 502 };
    }

    private bool AcceptResponse(BrowserResponse response)
    {
        if (response is { Status: 200, Cancelled: false } && !string.IsNullOrWhiteSpace(response.Body)) return true;
        // A transient connection/DB failure does not discard an already verified user's draft.
        if (_access is null || _accessBlocked || response.Status == 404) Deny(response.Cancelled ? 499 : response.Status, response.Body);
        return false;
    }

    private void Deny(int status, string? body = null)
    {
        if (_access is not null && (!_accessBlocked || _errorStatus != status))
            Logger.LogWarning("Access recheck for {Page} returned HTTP {Status}. The editor is blocked; its draft is retained.", RouteData.PageType.Name, status);
        _accessBlocked = true;
        _errorStatus = status;
        _errorBody = body;
    }
    private void Retry() { _pending = _requirement is not null; _errorStatus = null; _errorBody = null; _errorDomain = "profile"; }

    [JSInvokable]
    public Task RecheckPageAccess() => InvokeAsync(() =>
    {
        if (_disposed || _requirement is null) return;
        _pending = true;
        StateHasChanged();
    });

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        try
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            if (_watcher is not null)
            {
                await _watcher.InvokeVoidAsync("stop", cleanup.Token);
                await _watcher.DisposeAsync();
            }
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("cancelPageAccess", cleanup.Token, _requestKey);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException)
        {
            // A closing circuit cannot run browser cleanup; pagehide also stops the JS watcher.
        }
        catch (Exception exception) when (exception is JSException or OperationCanceledException) { }
        finally
        {
            _watcher = null;
            _module = null;
            _receiver?.Dispose();
            _receiver = null;
            _lifetime.Dispose();
        }
    }

    private sealed class BrowserResponse
    {
        public int Status { get; set; }
        public string Body { get; set; } = string.Empty;
        public bool Cancelled { get; set; }
    }
}
