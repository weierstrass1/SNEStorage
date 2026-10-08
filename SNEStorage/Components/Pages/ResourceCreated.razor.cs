using Microsoft.AspNetCore.Components;
using SNEStorage.Components.Shared;

namespace SNEStorage.Components.Pages;

[RequireAccount]
public partial class ResourceCreated : IDisposable
{
    private CancellationTokenSource? _redirect;
    private long _activeResourceId;

    [Parameter]
    public long ResourceId { get; set; }

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    protected string ResourceUrl => $"/Resource/{ResourceId}";

    protected override void OnParametersSet()
    {
        if (_activeResourceId != ResourceId)
        {
            _redirect?.Cancel();
            _redirect?.Dispose();
            _redirect = null;
            _activeResourceId = ResourceId;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_redirect is not null || ResourceId <= 0)
        {
            return;
        }

        _redirect = new CancellationTokenSource();
        var token = _redirect.Token;
        var url = ResourceUrl;
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(3), token);
            Navigation.NavigateTo(url);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Leaving the confirmation page cancels its redirect.
        }
    }

    public void Dispose()
    {
        _redirect?.Cancel();
        _redirect?.Dispose();
    }
}
