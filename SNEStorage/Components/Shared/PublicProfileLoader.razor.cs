using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SNEStorage.Models;

namespace SNEStorage.Components.Shared;

public partial class PublicProfileLoader : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _requestKey = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _lifetime = new();
    private IJSObjectReference? _module;
    private UserInfo? _profile;
    private string? _loadedUserId;
    private int? _errorStatus;
    private string? _errorBody;
    private int _generation;
    private bool _pending, _disposed;

    [Parameter] public string UserId { get; set; } = string.Empty;
    [Parameter, EditorRequired] public RenderFragment<UserInfo> ChildContent { get; set; } = default!;
    [Parameter] public RenderFragment? FallbackContent { get; set; }
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private ILogger<PublicProfileLoader> Logger { get; set; } = default!;

    protected override void OnParametersSet()
    {
        if (_loadedUserId == UserId) return;
        _loadedUserId = UserId;
        Retry();
    }

    private void Retry()
    {
        _profile = null;
        _errorStatus = null;
        _errorBody = null;
        _generation++;
        _pending = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_pending || _disposed) return;
        _pending = false;
        var generation = _generation;
        var userId = UserId;
        try
        {
            _module ??= await JavaScript.InvokeAsync<IJSObjectReference>("import", _lifetime.Token, "./js/profile.js?v=14");
            if (_disposed || generation != _generation) return;
            var json = await _module.InvokeAsync<string>("getProfile", _lifetime.Token,
                $"/api/profile/{Uri.EscapeDataString(userId)}", _requestKey, false);
            if (_disposed || generation != _generation) return;
            var response = JsonSerializer.Deserialize<BrowserResponse>(json, JsonOptions);
            if (response is { Status: 200, Cancelled: false })
                _profile = JsonSerializer.Deserialize<UserInfo>(response.Body, JsonOptions);
            _errorStatus = _profile is not null ? null : response is { Cancelled: true } ? 499 : response?.Status ?? 502;
            _errorBody = _profile is null ? response?.Body : null;
            if (_profile is null && _errorStatus == 200) _errorStatus = 502;
        }
        catch (Exception exception) when (exception is JSException or JsonException or OperationCanceledException)
        {
            if (!_disposed && generation == _generation)
            {
                Logger.LogDebug(exception, "The public profile could not be loaded.");
                _errorStatus = exception is JsonException ? 502 : 0;
            }
        }
        finally
        {
            if (!_disposed && generation == _generation) StateHasChanged();
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        await _lifetime.CancelAsync();
        if (_module is not null)
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try { await _module.InvokeVoidAsync("cancelProfileRequest", cleanup.Token, _requestKey); }
            catch (Exception exception) when (exception is JSException or OperationCanceledException) { }
            try { await _module.DisposeAsync(); }
            catch (Exception exception) when (exception is JSException or OperationCanceledException) { }
        }
        _lifetime.Dispose();
    }

    private sealed record BrowserResponse(int Status, string Body, bool Cancelled);
}
