using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace SNEStorage.Components.Shared;

public partial class MarkdownText : IAsyncDisposable
{
    private ElementReference _content;
    private IJSObjectReference? _module;
    private string? _source;
    private bool _sourceExcerpt;
    private bool _pending, _rendering, _ready, _disposed;

    [Parameter] public string? Text { get; set; }
    [Parameter] public bool Compact { get; set; }
    [Parameter] public int? MaxLines { get; set; }
    private bool IsExcerpt => MaxLines is > 0;
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private ILogger<MarkdownText> Logger { get; set; } = default!;

    protected override void OnParametersSet()
    {
        if (_source == (Text ?? string.Empty) && _sourceExcerpt == IsExcerpt) return;
        _source = Text ?? string.Empty;
        _sourceExcerpt = IsExcerpt;
        _pending = true;
        _ready = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || !_pending || _rendering) return;
        _pending = false;
        _rendering = true;
        var source = _source;
        var excerpt = _sourceExcerpt;
        try
        {
            var module = _module ?? await JavaScript.InvokeAsync<IJSObjectReference>(
                "import", "./js/markdown.js?v=3");
            if (_disposed)
            {
                await module.DisposeAsync();
                return;
            }
            _module = module;
            await module.InvokeVoidAsync("renderMarkdown", _content, source, excerpt);
            if (!_disposed && source == _source && excerpt == _sourceExcerpt) _ready = true;
        }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) { }
        catch (JSException exception)
        {
            Logger.LogWarning(exception, "Markdown formatting could not be loaded; displaying plain text.");
        }
        finally
        {
            _rendering = false;
            if (!_disposed) await InvokeAsync(StateHasChanged);
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (_module is null) return;
        try { await _module.DisposeAsync(); }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) { }
    }
}
