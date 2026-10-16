using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace SNEStorage.Components.Shared;

// Read public presentation files locally, before rendering, without an API or a shared cache.
public abstract class StaticDocumentPage<TDocument> : ComponentBase, IAsyncDisposable where TDocument : class
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly CancellationTokenSource _lifetime = new();
    private IJSObjectReference? _module;
    private bool _disposed;
    private bool _scrollToAnchor;
    protected TDocument? _document;
    protected string? _error;
    protected bool _loading = true;

    protected abstract string DocumentPath { get; }
    protected abstract string LoadErrorMessage { get; }
    protected virtual void OnDocumentLoaded(TDocument document) { }

    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private IWebHostEnvironment Environment { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private ILogger<StaticDocumentPage<TDocument>> Logger { get; set; } = default!;

    protected override Task OnInitializedAsync() => LoadAsync();

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_scrollToAnchor || _disposed) return;
        _scrollToAnchor = false;
        // Normal navigation needs no JS interop. Only deep links need a scroll after render.
        if (!Uri.TryCreate(Navigation.Uri, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Fragment)) return;
        try
        {
            var module = await JavaScript.InvokeAsync<IJSObjectReference>("import", _lifetime.Token, "./js/site-rules.js?v=4");
            if (_disposed) { await module.DisposeAsync(); return; }
            _module = module;
            await module.InvokeVoidAsync("scrollToAnchor", _lifetime.Token);
        }
        catch (Exception exception) when (exception is JSException or OperationCanceledException)
        { Logger.LogDebug(exception, "The public document anchor could not be scrolled into view."); }
    }

    protected async Task LoadAsync()
    {
        if (_disposed) return;
        _loading = true;
        _error = null;
        try
        {
            var file = Environment.WebRootFileProvider.GetFileInfo(DocumentPath);
            if (!file.Exists) throw new FileNotFoundException("The public document was not found.", DocumentPath);
            await using var stream = file.CreateReadStream();
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: _lifetime.Token);
            if (!json.RootElement.TryGetProperty("sections", out var sections) ||
                sections.ValueKind != JsonValueKind.Array || sections.GetArrayLength() == 0)
                throw new JsonException("The public document needs at least one section.");
            var document = json.Deserialize<TDocument>(JsonOptions)
                ?? throw new JsonException("The public document is empty.");
            if (!_disposed)
            {
                OnDocumentLoaded(document);
                _document = document;
                _scrollToAnchor = true;
            }
        }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception)
        {
            if (!_disposed)
            {
                Logger.LogWarning(exception, "The public document {DocumentPath} could not be loaded.", DocumentPath);
                _error = LoadErrorMessage;
            }
        }
        finally
        {
            if (!_disposed) _loading = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _lifetime.Cancel();
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (Exception exception) when (exception is JSException or OperationCanceledException) { }
        }
        _lifetime.Dispose();
    }
}
