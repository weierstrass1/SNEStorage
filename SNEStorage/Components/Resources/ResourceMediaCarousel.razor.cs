using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using SNEStorage.Models;

namespace SNEStorage.Components.Resources;

public partial class ResourceMediaCarousel : IAsyncDisposable
{
    private readonly string _requestKey = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _lifetime = new();
    private IJSObjectReference? _module;
    private int _activeIndex;
    private long _previousResourceId;
    private long _previousFileId;
    private bool _playRequested;
    private bool _failed;
    private bool _loading;
    private bool _needsLoad;
    private bool _disposed;
    private int _generation;
    private string? _sourceUrl;
    private string? _errorMessage;
    private PreviewKind? _loadedKind;

    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private ILogger<ResourceMediaCarousel> Logger { get; set; } = default!;

    [Parameter] public long ResourceId { get; set; }
    [Parameter] public string ResourceName { get; set; } = "Resource";
    [Parameter] public IReadOnlyList<ResourcePreview> Previews { get; set; } = [];
    [Parameter] public bool LoadMedia { get; set; } = true;

    private ResourcePreview? Active => Previews.Count == 0 ? null : Previews[_activeIndex];
    protected long ActiveFileId => Active is { FileId: > 0 } preview ? preview.FileId : Active?.File?.Id ?? 0;
    protected string? ActiveUrl => _sourceUrl;
    protected PreviewKind Kind => _loadedKind ?? PreviewKind.Other;

    protected override void OnParametersSet()
    {
        if (_previousResourceId != ResourceId) _activeIndex = 0;
        _activeIndex = Math.Clamp(_activeIndex, 0, Math.Max(0, Previews.Count - 1));
        if (_previousResourceId != ResourceId || _previousFileId != ActiveFileId)
        {
            ResetPreview();
            _previousResourceId = ResourceId;
            _previousFileId = ActiveFileId;
        }
    }

    protected void Select(int index)
    {
        _activeIndex = index;
        _previousFileId = ActiveFileId;
        ResetPreview();
    }
    protected void Previous() => Select((_activeIndex + Previews.Count - 1) % Previews.Count);
    protected void Next() => Select((_activeIndex + 1) % Previews.Count);
    protected void Play()
    {
        _playRequested = true;
        _loading = _needsLoad = true;
        _generation++;
    }
    protected void MediaFailed() => _failed = true;
    protected void RetryMedia() => ResetPreview();

    private void ResetPreview()
    {
        _generation++;
        _sourceUrl = _errorMessage = null;
        _loadedKind = null;
        _playRequested = _failed = false;
        _loading = _needsLoad = Previews.Count > 0;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || !LoadMedia || !_needsLoad) return;
        _needsLoad = false;
        var generation = _generation;
        var fileId = ActiveFileId;
        var metadataUrl = ResourceFileLinks.For(ResourceId, fileId);
        var downloadUrl = ResourceFileLinks.For(ResourceId, fileId, true);
        try
        {
            if (metadataUrl is null || downloadUrl is null) { _failed = true; return; }
            _module ??= await JavaScript.InvokeAsync<IJSObjectReference>("import", _lifetime.Token, "./js/resource-media.js?v=3");
            if (_disposed || generation != _generation) return;
            // Pass only file metadata already supplied by ResourceController, without navigation graphs.
            var knownFile = Active?.File is { } file ? new
            {
                file.Id, file.URL, FileType = new { Value = file.FileType?.Value }
            } : null;
            var response = await _module.InvokeAsync<PreviewResponse>("loadPreview", _lifetime.Token,
                metadataUrl, downloadUrl, fileId, _requestKey, _playRequested, knownFile);
            if (_disposed || generation != _generation) return;
            if (response.Cancelled) { _failed = true; return; }
            if (response.Status is < 200 or >= 300)
            {
                _failed = true;
                _errorMessage = response.Message ?? ApiErrorPresentation.Describe(response.Status, response.Body, "resource").Message;
                return;
            }
            _loadedKind = response.Kind switch
            {
                "image" => PreviewKind.Image, "video" => PreviewKind.Video, _ => PreviewKind.Other
            };
            _sourceUrl = response.Url;
        }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception) when (exception is JSException or OperationCanceledException)
        {
            if (!_disposed && generation == _generation)
            {
                Logger.LogWarning(exception, "Preview {FileId} could not be loaded.", fileId);
                _failed = true;
            }
        }
        finally
        {
            if (!_disposed && generation == _generation)
            {
                _loading = false;
                StateHasChanged();
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _generation++;
        _lifetime.Cancel();
        if (_module is not null)
        {
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _module.InvokeVoidAsync("releasePreview", cleanup.Token, _requestKey);
                await _module.DisposeAsync();
            }
            catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
        }
        _lifetime.Dispose();
    }

    protected void HandleKeyDown(KeyboardEventArgs args)
    {
        if (Previews.Count < 2 || _playRequested) return;
        if (args.Key == "ArrowLeft") Previous();
        else if (args.Key == "ArrowRight") Next();
    }

    protected enum PreviewKind { Image, Video, Other }

    private sealed class PreviewResponse
    {
        public int Status { get; set; }
        public string? Body { get; set; }
        public string? Message { get; set; }
        public string? Kind { get; set; }
        public string? Url { get; set; }
        public bool Cancelled { get; set; }
    }
}
