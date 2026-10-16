using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace SNEStorage.Components.Resources;

public partial class ResourceAudioPreview : IAsyncDisposable
{
    [Parameter] public string SourceUrl { get; set; } = "";
    [Parameter] public string Kind { get; set; } = "audio";
    [Parameter] public string FileName { get; set; } = "Sound preview";
    [Parameter] public bool Enabled { get; set; } = true;
    [Parameter] public bool Compact { get; set; }
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    private readonly string _playerId = "sound-preview-" + Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _lifetime = new();
    private IJSObjectReference? _module;
    private string? _previousSource;
    private string? _previousKind;
    private bool _previousEnabled;
    private bool _pending;
    private bool _disposed;
    private string? _error;

    protected override void OnParametersSet()
    {
        if (_previousSource == SourceUrl && _previousKind == Kind && _previousEnabled == Enabled) return;
        _previousSource = SourceUrl;
        _previousKind = Kind;
        _previousEnabled = Enabled;
        _pending = true;
        _error = null;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || !_pending) return;
        _pending = false;
        try
        {
            _module ??= await JavaScript.InvokeAsync<IJSObjectReference>("import", _lifetime.Token, "./js/audio-preview.js?v=4");
            if (_disposed) return;
            if (Enabled && !string.IsNullOrWhiteSpace(SourceUrl))
                await _module.InvokeVoidAsync("initializeAudioPreview", _lifetime.Token, _playerId, SourceUrl, Kind, FileName);
            else await _module.InvokeVoidAsync("disposeAudioPreview", _lifetime.Token, _playerId);
        }
        catch (Exception exception) when (exception is JSDisconnectedException or OperationCanceledException) { }
        catch (JSException)
        {
            if (_disposed) return;
            _error = "The sound player could not be loaded. Reload the page and try again.";
            StateHasChanged();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        if (_module is not null)
        {
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _module.InvokeVoidAsync("disposeAudioPreview", cleanup.Token, _playerId);
                await _module.DisposeAsync();
            }
            catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
        }
        _lifetime.Dispose();
    }
}
