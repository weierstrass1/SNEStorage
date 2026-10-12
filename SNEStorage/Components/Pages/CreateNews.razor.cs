using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SNEStorage.Components.News;
using SNEStorage.Components.Shared;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Pages;

[RequireAccount(StaffOnly = true)]
public partial class CreateNews : IAsyncDisposable
{
    private readonly NewsCreateDTO _form = new() { Title = "", Text = "" };
    private readonly string _requestKey = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _lifetime = new();
    private IJSObjectReference? _module;
    private ErrorCopy? _error;
    private string[] _validationErrors = [];
    private bool _saving, _disposed;

    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    private async Task SubmitAsync()
    {
        if (_saving || _disposed) return;
        _saving = true;
        _error = null;
        _validationErrors = [];
        try
        {
            _module ??= await JavaScript.InvokeAsync<IJSObjectReference>("import", _lifetime.Token, "./js/news.js?v=5");
            if (_disposed) return;
            var response = await new NewsApiClient(_module, _requestKey).CreateAsync(_form, _lifetime.Token);
            if (_disposed) return;
            if (response.Status is >= 200 and < 300 && long.TryParse(response.Body, out var newsId) && newsId > 0)
            {
                Navigation.NavigateTo($"/News#news-{newsId}");
                return;
            }
            _error = response.Status is 0 or 408 or 499 or 504 or (>= 200 and < 300) ?
                new("Response unavailable", "We could not confirm whether your news was published. Check the news page before submitting again. Your text has been kept.") :
                ApiErrorPresentation.Describe(response.Status, response.Body, "news");
            _validationErrors = ApiErrorPresentation.ValidationErrors(response.Body);
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException)
        {
            if (!_disposed) _error = new("Request interrupted", "We could not confirm whether your news was published. Check the news page before submitting again. Your text has been kept.");
        }
        finally { _saving = false; }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _lifetime.Cancel();
        if (_module is not null)
        {
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _module.InvokeVoidAsync("dispose", cleanup.Token, _requestKey);
                await _module.DisposeAsync();
            }
            catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
        }
        _lifetime.Dispose();
    }
}
