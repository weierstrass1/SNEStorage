using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace SNEStorage.Components.Shared;

public partial class AvatarPicker : IAsyncDisposable
{
    [Parameter] public string InputId { get; set; } = "avatar";
    [Parameter] public string FieldName { get; set; } = "Avatar";
    [Parameter] public bool Busy { get; set; }
    [Parameter] public bool Required { get; set; } = true;
    [Parameter] public string? InitialAvatarUrl { get; set; }
    [Parameter] public EventCallback<bool> SelectionChanged { get; set; }
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    private string PickerId => InputId + "-picker";
    private IJSObjectReference? _module;
    private bool _disposed;

    private Task AvatarSelectionChanged(ChangeEventArgs args) =>
        SelectionChanged.InvokeAsync(!string.IsNullOrEmpty(args.Value?.ToString()));

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed) return;
        try
        {
            _module = await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/avatar-picker.js?v=3");
            if (!_disposed) await _module.InvokeVoidAsync("initializeAvatarPicker", PickerId);
        }
        catch (Exception exception) when (exception is JSDisconnectedException or OperationCanceledException) { }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (_module is null) return;
        try
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await _module.InvokeVoidAsync("disposeAvatarPicker", cleanup.Token, PickerId);
            await _module.DisposeAsync();
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
    }
}
