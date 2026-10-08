using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace SNEStorage.Components.Resources;

public partial class ResourcePicker : IAsyncDisposable
{
    private IJSObjectReference? _module;
    private DotNetObjectReference<ResourcePicker>? _reference;
    private bool _disposed;
    private bool _isOpen;
    private bool _openedByHover;
    private bool _syncPending;
    private bool _focusSearch;
    private bool _restoreFocus;

    [Parameter, EditorRequired] public string PickerId { get; set; } = string.Empty;
    [Parameter, EditorRequired] public string Label { get; set; } = string.Empty;
    [Parameter] public bool Required { get; set; }
    [Parameter] public bool OpenOnHover { get; set; }
    [Parameter] public bool FocusSearch { get; set; }
    [Parameter] public RenderFragment? SummaryContent { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private ILogger<ResourcePicker> Logger { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed) return;
        try
        {
            if (firstRender)
            {
                var module = await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/resource-pickers.js?v=4");
                if (_disposed) { await module.DisposeAsync(); return; }
                _module = module;
                _reference = DotNetObjectReference.Create(this);
                await _module.InvokeVoidAsync("initializePicker", PickerId, _reference);
            }
            if (_module is not null && _syncPending)
            {
                var focusSearch = _focusSearch;
                var restoreFocus = _restoreFocus;
                _syncPending = _focusSearch = _restoreFocus = false;
                await _module.InvokeVoidAsync("syncPicker", PickerId, focusSearch, restoreFocus);
            }
        }
        catch (JSDisconnectedException) { }
        catch (JSException exception)
        {
            Logger.LogWarning(exception, "Resource picker enhancement could not be initialized.");
        }
    }

    protected void Toggle()
    {
        // A click pins a hover-open menu instead of immediately hiding it.
        if (_isOpen && _openedByHover) { _openedByHover = false; return; }
        _isOpen = !_isOpen;
        _openedByHover = false;
        _syncPending = true;
        _focusSearch = _isOpen && FocusSearch;
        _restoreFocus = false;
    }

    protected void OpenFromHover()
    {
        if (!OpenOnHover || _isOpen) return;
        _isOpen = _openedByHover = _syncPending = true;
    }

    protected Task KeyDownAsync(KeyboardEventArgs args) =>
        args.Key == "Escape" && _isOpen ? CloseAsync() : Task.CompletedTask;

    public Task CloseAsync() => CloseAsync(restoreFocus: true);

    [JSInvokable]
    public Task DismissAsync() => CloseAsync(restoreFocus: false);

    private Task CloseAsync(bool restoreFocus)
    {
        if (_disposed || !_isOpen) return Task.CompletedTask;
        _isOpen = _openedByHover = _focusSearch = false;
        _syncPending = true;
        _restoreFocus = restoreFocus;
        return InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("disposePicker", PickerId);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { }
        finally { _reference?.Dispose(); }
    }
}
