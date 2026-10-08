using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace SNEStorage.Components.Resources;

public partial class ResourcePreviewPicker : IAsyncDisposable
{
    [Parameter] public string InputId { get; set; } = "resource-previews";
    [Parameter] public int MaxFiles { get; set; } = 20;
    private IJSObjectReference? _module;
    private List<PreviewItem> _items = [];
    private List<string> _errors = [];
    private bool _ready;
    private bool _disposed;
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private ILogger<ResourcePreviewPicker> Logger { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed) return;
        try
        {
            var module = await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/resource-previews.js?v=2");
            if (_disposed) { await module.DisposeAsync(); return; }
            _module = module;
            _ready = true;
        }
        catch (JSException exception)
        {
            Logger.LogWarning(exception, "Preview uploads could not be initialized.");
            _errors = ["Preview uploads could not be loaded. Please reload the page."];
        }
        if (!_disposed) StateHasChanged();
    }

    private static string FormatSize(long size) => size >= 1024 * 1024
        ? $"{size / (1024d * 1024d):0.##} MB" : $"{size / 1024d:0.##} KB";

    private Task FilesSelectedAsync(InputFileChangeEventArgs args) => UpdateAsync("addPreviews", InputId, Math.Clamp(MaxFiles, 0, 20));
    private Task RemoveAsync(string id) => UpdateAsync("removePreview", InputId, id);

    private async Task UpdateAsync(string method, params object[] args)
    {
        if (_module is null || _disposed) return;
        try
        {
            var selection = await _module.InvokeAsync<PreviewSelection>(method, args);
            if (_disposed) return;
            _items = selection.Items;
            _errors = selection.Errors;
        }
        catch (JSException exception)
        {
            Logger.LogWarning(exception, "Preview selection failed.");
            _errors = ["The preview selection could not be updated. Please try again."];
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (_module is null) return;
        try
        {
            await _module.InvokeVoidAsync("disposePreviews", InputId);
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
    }

    private sealed class PreviewSelection
    {
        public List<PreviewItem> Items { get; set; } = [];
        public List<string> Errors { get; set; } = [];
    }
    private sealed class PreviewItem
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public long Size { get; set; }
        public string Url { get; set; } = string.Empty;
        public bool Video { get; set; }
    }
}
