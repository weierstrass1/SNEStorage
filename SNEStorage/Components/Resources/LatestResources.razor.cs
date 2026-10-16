using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Resources;

public partial class LatestResources : IAsyncDisposable
{
    private readonly string _requestKey = Guid.NewGuid().ToString("N");
    private IJSObjectReference? _module;
    private IReadOnlyList<ResourceFinderViewDTO> _resources = [];
    private string? _error;
    private bool _loading = true, _disposed;

    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private ILogger<LatestResources> Logger { get; set; } = default!;
    [Parameter] public bool Compact { get; set; }

    protected override Task OnAfterRenderAsync(bool firstRender) => firstRender ? LoadAsync() : Task.CompletedTask;

    private async Task LoadAsync()
    {
        if (_disposed) return;
        _loading = true;
        _error = null;
        try
        {
            _module ??= await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/resource-list.js?v=4");
            if (_disposed) { await _module.DisposeAsync(); return; }
            var result = await new ResourceListApiClient(_module, _requestKey).FindLatestPublicAsync();
            if (_disposed) return;
            if (result.Success) _resources = result.Data!;
            else _error = "Latest resources could not be loaded. Please try again.";
        }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception) when (exception is JSException or JsonException)
        {
            if (!_disposed)
            {
                Logger.LogWarning(exception, "Latest resources could not be loaded.");
                _error = "Latest resources could not be loaded. Please try again.";
            }
        }
        finally
        {
            if (!_disposed) { _loading = false; await InvokeAsync(StateHasChanged); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (_module is null) return;
        try
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await _module.InvokeVoidAsync("cancelPublicList", cleanup.Token, _requestKey);
            await _module.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) { }
    }
}
