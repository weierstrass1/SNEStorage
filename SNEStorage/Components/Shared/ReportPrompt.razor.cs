using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;

namespace SNEStorage.Components.Shared;

public partial class ReportPrompt : IAsyncDisposable
{
    private IJSObjectReference? _module;
    private bool _disposed;
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    protected override void OnInitialized() => Navigation.LocationChanged += LocationChanged;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed) return;
        try
        {
            var module = await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/report.js?v=2");
            if (_disposed) { await module.DisposeAsync(); return; }
            _module = module;
            await module.InvokeVoidAsync("initialize");
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException or ObjectDisposedException) { }
    }

    private async void LocationChanged(object? sender, LocationChangedEventArgs args)
    {
        if (_disposed || _module is null) return;
        try { await _module.InvokeVoidAsync("close"); }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException or ObjectDisposedException) { }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        Navigation.LocationChanged -= LocationChanged;
        if (_module is null) return;
        try
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await _module.InvokeVoidAsync("dispose", cleanup.Token);
            await _module.DisposeAsync();
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException or ObjectDisposedException) { }
    }
}
