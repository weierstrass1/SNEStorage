using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SNEStorage.Models;

namespace SNEStorage.Components.Resources;

public partial class ResourceMediaCarousel : IAsyncDisposable
{
    private readonly string _carouselId = "resource-carousel-" + Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _lifetime = new();
    private IJSObjectReference? _module;
    private object? _configuration;
    private string? _configurationKey;
    private bool _pending, _initializing, _disposed;
    private string? _initializationError;
    private IReadOnlyList<ResourcePreview>? _preparedPreviews;
    private long _preparedResourceId;
    private string? _preparedName;
    private bool _preparedLoadMedia;
    private string AudioPlayerId => _carouselId + "-audio";

    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private ILogger<ResourceMediaCarousel> Logger { get; set; } = default!;
    [Parameter] public long ResourceId { get; set; }
    [Parameter] public string ResourceName { get; set; } = "Resource";
    [Parameter] public IReadOnlyList<ResourcePreview> Previews { get; set; } = [];
    [Parameter] public bool LoadMedia { get; set; } = true;

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_preparedPreviews, Previews) && _preparedResourceId == ResourceId &&
            _preparedName == ResourceName && _preparedLoadMedia == LoadMedia) return;
        _preparedPreviews = Previews;
        _preparedResourceId = ResourceId;
        _preparedName = ResourceName;
        _preparedLoadMedia = LoadMedia;
        var configuration = new
        {
            ResourceId, ResourceName, Enabled = LoadMedia, AudioPlayerId,
            Files = Previews.Select(preview =>
            {
                long fileId = preview.FileId > 0 ? preview.FileId : preview.File?.Id ?? 0;
                return new
                {
                    FileId = fileId,
                    MetadataUrl = ResourceFileLinks.For(ResourceId, fileId),
                    DownloadUrl = ResourceFileLinks.For(ResourceId, fileId, true),
                    FileName = Path.GetFileName((preview.File?.URL ?? "Preview").Replace('\\', '/')),
                    KnownFile = preview.File is { } file ? new
                    {
                        file.Id, file.URL, FileType = new { Value = file.FileType?.Value }
                    } : null
                };
            }).ToArray()
        };
        string key = JsonSerializer.Serialize(configuration);
        if (_configurationKey == key) return;
        _configuration = configuration;
        _configurationKey = key;
        _pending = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || !_pending || _initializing) return;
        _pending = false;
        _initializing = true;
        var configuration = _configuration;
        try
        {
            var module = _module ?? await JavaScript.InvokeAsync<IJSObjectReference>(
                "import", _lifetime.Token, "./js/resource-carousel.js?v=1");
            if (_disposed)
            {
                if (_module is null) await module.DisposeAsync();
                return;
            }
            _module = module;
            await _module.InvokeVoidAsync("initializeCarousel", _lifetime.Token, _carouselId, configuration);
        }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) when (_disposed) { }
        catch (JSException exception)
        {
            if (_disposed) return;
            Logger.LogWarning(exception, "The resource preview carousel could not be initialized.");
            _initializationError = "The preview viewer could not start. Reload the page to try again.";
            StateHasChanged();
        }
        finally
        {
            _initializing = false;
            if (!_disposed && _pending) StateHasChanged();
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
                await _module.InvokeVoidAsync("disposeCarousel", cleanup.Token, _carouselId);
                await _module.DisposeAsync();
            }
            catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
        }
        _lifetime.Dispose();
    }
}
