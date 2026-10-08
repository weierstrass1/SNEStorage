using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using SNEStorage.Components.Resources;
using SNEStorage.Models;

namespace SNEStorage.Components.Pages;

public partial class ResourceDetails : IAsyncDisposable
{
    protected static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-US");
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex HexColorPattern = new("\\A#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})\\z", RegexOptions.Compiled);
    private readonly string _requestKey = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _lifetime = new();
    private IJSObjectReference? _module;
    private IJSObjectReference? _accountWatcher;
    private DotNetObjectReference<ResourceDetails>? _accountReceiver;
    private Resource? _resource;
    private long? _requestedId;
    private int _generation;
    private bool _loading = true;
    private bool _disposed;
    private int? _errorStatus;
    private string? _errorBody;
    private bool _showVersions;
    private bool _isAuthenticated;
    private bool _canModerateComments;
    private string? _currentUserId;
    private bool _accountRefreshPending = true;
    private int _accountGeneration;
    private bool _accountVerifiedOnce;
    private string? _verifiedUserId;

    [Parameter] public long ResourceId { get; set; }
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private ILogger<ResourceDetails> Logger { get; set; } = default!;
    [Inject] private AuthenticationStateProvider Authentication { get; set; } = default!;

    protected string ResourceName => string.IsNullOrWhiteSpace(_resource?.Name) ? "Resource" : _resource.Name;
    protected IReadOnlyList<ResourceCategory> Categories => _resource?.ResourceResourceTypes
        .Select(relation => relation.ResourceType?.Category)
        .OfType<ResourceCategory>()
        .Where(category => !string.IsNullOrWhiteSpace(category.Name))
        .DistinctBy(category => category.Id)
        .OrderBy(category => category.Name, StringComparer.OrdinalIgnoreCase)
        .ToArray() ?? [];
    protected IReadOnlyList<ResourcePreview> Previews => _resource?.ResourcePreviews?.ToArray() ?? [];
    protected IReadOnlyList<ResourceVersion> Versions => _resource?.ResourceVersions?.ToArray() ?? [];
    protected ResourceVersion? LatestVersion => Versions.OrderByDescending(version => version.CreatedAt).ThenByDescending(version => version.Id).FirstOrDefault();
    protected string? LatestDownload => LatestVersion is { } version ? ResourceFileLinks.For(ResourceId, version.FileId > 0 ? version.FileId : version.File?.Id ?? 0, true) : null;
    protected long Downloads => Versions.Sum(version => (long)version.Downloads);
    protected bool CanManage => _isAuthenticated && ResourceManagementAccess.CanDisplay(_resource, _currentUserId);
    protected bool HasQualitySeal => LatestVersion?.SealOfQuality == true;
    protected IReadOnlyList<string> ContentWarnings => _resource is { } resource
        ? new (bool Included, string Label)[]
        {
            (resource.IncludesPorn, "Sexual content"), (resource.IncludesGore, "Gore"),
            (resource.IncludesPolitics, "Politics"), (resource.IncludesSlurs, "Slurs"),
            (resource.IncludesSensitiveContent, "Sensitive content")
        }.Where(flag => flag.Included).Select(flag => flag.Label).ToArray()
        : [];

    protected static string FormatDate(DateTime date) => date.ToString("MMM d, yyyy", DisplayCulture);
    protected static string AuthorName(ResourceAuthors author) =>
        string.IsNullOrWhiteSpace(author.UserSummary?.Username) ? "SNEStorage author" : author.UserSummary.Username;
    protected static string? AuthorProfileUrl(ResourceAuthors author) =>
        string.IsNullOrWhiteSpace(author.UserSummary?.Username)
            ? null
            : $"/Profile/{Uri.EscapeDataString(author.UserSummary.Username)}";
    protected static string AuthorColor(ResourceAuthors author) =>
        UsernameColor(author.UserSummary?.UsernameColor);
    private static string UsernameColor(string? color) =>
        color is not null && HexColorPattern.IsMatch(color) ? color : "#e6d2fb";
    protected static string VersionLabel(ResourceVersion version) => string.IsNullOrWhiteSpace(version.Version) ? "Latest release" : version.Version;
    protected static IReadOnlyList<(string Name, string Url)> MirrorLinks(ResourceExternalDependency dependency) =>
        new[] { dependency.Mirror1, dependency.Mirror2, dependency.Mirror3, dependency.Mirror4 }
            .Select((url, index) => (Name: $"Mirror {index + 1}", Url: ResourceFileLinks.External(url)))
            .Where(mirror => mirror.Url is not null).Select(mirror => (mirror.Name, mirror.Url!)).ToArray();

    protected override void OnInitialized()
    {
        Authentication.AuthenticationStateChanged += AuthenticationChanged;
    }
    private void InvalidateAccount()
    {
        _accountGeneration++;
        _accountRefreshPending = true;
        _isAuthenticated = false;
        _canModerateComments = false;
        _currentUserId = null;
    }
    [JSInvokable]
    public Task RecheckPageAccess() => InvokeAsync(() =>
    {
        if (_disposed) return;
        InvalidateAccount();
        StateHasChanged();
    });
    private async void AuthenticationChanged(Task<AuthenticationState> stateTask)
    {
        try
        {
            await stateTask;
            if (_disposed) return;
            await InvokeAsync(() => { if (_disposed) return; InvalidateAccount(); StateHasChanged(); });
        }
        catch (Exception exception) when (exception is InvalidOperationException or OperationCanceledException)
        {
            if (!_disposed) Logger.LogDebug(exception, "Resource account state could not be refreshed.");
        }
    }

    private async Task RefreshAccountAsync()
    {
        if (_module is null || _disposed) return;
        _accountRefreshPending = false;
        var generation = _accountGeneration;
        try
        {
            var json = await _module.InvokeAsync<string>("getResourceAccount", _lifetime.Token, _requestKey);
            if (_disposed || generation != _accountGeneration) return;
            var response = JsonSerializer.Deserialize<BrowserResponse>(json, JsonOptions);
            var profile = response is { Status: 200, Cancelled: false } && !string.IsNullOrWhiteSpace(response.Body)
                ? JsonSerializer.Deserialize<UserInfo>(response.Body, JsonOptions) : null;
            var principal = profile is null ? null : (await Authentication.GetAuthenticationStateAsync()).User;
            if (_disposed || generation != _accountGeneration) return;
            if (profile is not null || response is { Status: 401, Cancelled: false })
            {
                // Session changes also require the controller to recheck access to the resource itself.
                if (_resource is not null && _accountVerifiedOnce && _verifiedUserId != profile?.UserId)
                {
                    _resource = null;
                    _requestedId = null;
                    _loading = true;
                }
                _verifiedUserId = profile?.UserId;
                _accountVerifiedOnce = true;
            }
            _currentUserId = profile?.UserId;
            _isAuthenticated = !string.IsNullOrWhiteSpace(_currentUserId);
            _canModerateComments = _isAuthenticated && principal?.Identity?.IsAuthenticated == true &&
                principal.FindFirstValue(ClaimTypes.NameIdentifier) == _currentUserId &&
                (principal.HasClaim("Admin", "1") || principal.HasClaim("Moderator", "1"));
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or JsonException or OperationCanceledException)
        {
            if (!_disposed && generation == _accountGeneration)
            {
                _isAuthenticated = false;
                _canModerateComments = false;
                _currentUserId = null;
                Logger.LogDebug(exception, "Resource management links could not be verified.");
            }
        }
    }

    protected Task ShowOverview() => SetTabAsync(false);
    protected Task ShowVersions() => SetTabAsync(true);
    private async Task SetTabAsync(bool versions, bool focus = false)
    {
        _showVersions = versions;
        if (_module is null || _disposed) return;
        try { await _module.InvokeVoidAsync("selectResourceTab", _lifetime.Token, versions, focus); }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) when (_disposed) { }
    }
    protected Task HandleTabKeyDown(KeyboardEventArgs args) => args.Key switch
    {
        "ArrowLeft" or "ArrowRight" => SetTabAsync(!_showVersions, true),
        "Home" => SetTabAsync(false, true), "End" => SetTabAsync(true, true), _ => Task.CompletedTask
    };

    protected override void OnParametersSet()
    {
        if (_requestedId == ResourceId) return;
        _savingRating = false;
        _ratingMessage = null;
        _savingComment = false;
        _commentMessage = null;
        _commentSubmissionRevision = 0;
        _publishedComment = null;
        _resource = null;
        _errorStatus = null;
        _errorBody = null;
        _loading = true;
        _showVersions = false;
        InvalidateAccount();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed) return;
        if (_requestedId == ResourceId)
        {
            if (_accountRefreshPending && _module is not null)
            {
                await RefreshAccountAsync();
                if (!_disposed) StateHasChanged();
            }
            return;
        }
        _requestedId = ResourceId;
        _loading = true;
        _resource = null;
        _errorStatus = null;
        _errorBody = null;
        var generation = ++_generation;
        var requestedId = ResourceId;
        Task? accountLoad = null;
        StateHasChanged();
        try
        {
            if (requestedId <= 0) { _errorStatus = 404; return; }
            _module ??= await JavaScript.InvokeAsync<IJSObjectReference>("import", _lifetime.Token, "./js/resource-details.js?v=8");
            if (_disposed || generation != _generation) return;
            await _module.InvokeVoidAsync("cancelResourceInteractions", _lifetime.Token, _requestKey);
            if (_accountWatcher is null)
            {
                _accountReceiver = DotNetObjectReference.Create(this);
                _accountWatcher = await _module.InvokeAsync<IJSObjectReference>("watchPageAccess", _lifetime.Token, _accountReceiver);
            }
            // Account controls load independently; the public resource content does not wait for them.
            accountLoad = RefreshAccountAsync();
            var json = await _module.InvokeAsync<string>("getResource", _lifetime.Token, requestedId, _requestKey);
            if (_disposed || generation != _generation || requestedId != ResourceId) return;
            var response = JsonSerializer.Deserialize<BrowserResponse>(json, JsonOptions);
            if (response is null) { _errorStatus = 502; return; }
            if (response.Cancelled) { _errorStatus = 499; return; }
            if (response.Status is >= 200 and < 300 && !string.IsNullOrWhiteSpace(response.Body))
            {
                _resource = JsonSerializer.Deserialize<Resource>(response.Body, JsonOptions);
                if (_resource is null || _resource.Id != requestedId)
                {
                    _resource = null;
                    _errorStatus = 502;
                }
            }
            else
            {
                _errorStatus = response.Status;
                _errorBody = response.Body;
            }
        }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception) when (exception is JSException or JsonException or OperationCanceledException)
        {
            if (generation == _generation && !_disposed)
            {
                Logger.LogWarning(exception, "Resource {ResourceId} could not be loaded.", requestedId);
                _errorStatus = exception is JsonException ? 502 : 0;
            }
        }
        finally
        {
            if (!_disposed && generation == _generation)
            {
                _loading = _requestedId != ResourceId;
                StateHasChanged();
            }
            if (accountLoad is not null) await accountLoad;
            if (!_disposed && generation == _generation) StateHasChanged();
        }
    }

    protected void Reload() { _requestedId = null; }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        Authentication.AuthenticationStateChanged -= AuthenticationChanged;
        _generation++;
        _lifetime.Cancel();
        if (_module is not null)
        {
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                if (_accountWatcher is not null) { await _accountWatcher.InvokeVoidAsync("stop", cleanup.Token); await _accountWatcher.DisposeAsync(); }
                await _module.InvokeVoidAsync("cancelResourceRequest", cleanup.Token, _requestKey);
                await _module.DisposeAsync();
            }
            catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
        }
        _accountReceiver?.Dispose();
        _lifetime.Dispose();
    }

    private sealed class BrowserResponse
    {
        public int Status { get; set; }
        public string? Body { get; set; }
        public bool Cancelled { get; set; }
    }
}
