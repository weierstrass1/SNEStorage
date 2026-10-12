using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SNEStorage.DTOs;

namespace SNEStorage.Components.News;

public partial class NewsEditForm : IAsyncDisposable
{
    // The creation DTO supplies the same required title/text validation for this editor.
    private readonly NewsCreateDTO _form = new() { Title = "", Text = "" };
    private readonly string _requestKey = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _lifetime = new();
    private IJSObjectReference? _module;
    private ErrorCopy? _error;
    private string[] _validationErrors = [];
    private bool _saving, _disposed;

    [Parameter, EditorRequired] public NewsReadDTO Item { get; set; } = default!;
    [Parameter] public EventCallback<NewsEditDTO> OnSaved { get; set; }
    [Parameter] public EventCallback OnCancel { get; set; }
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;

    protected override void OnInitialized()
    {
        _form.Title = Item.Title;
        _form.Text = Item.Text;
    }

    private async Task SaveAsync()
    {
        if (_saving || _disposed) return;
        _saving = true;
        _error = null;
        _validationErrors = [];
        var dto = new NewsEditDTO { Title = _form.Title.Trim(), Text = _form.Text.Trim() };
        try
        {
            _module ??= await JavaScript.InvokeAsync<IJSObjectReference>("import", _lifetime.Token, "./js/news.js?v=5");
            if (_disposed) return;
            var response = await new NewsApiClient(_module, _requestKey).EditAsync(Item.Id, dto, _lifetime.Token);
            if (_disposed) return;
            if (response.Status is >= 200 and < 300 && long.TryParse(response.Body, out var newsId) && newsId == Item.Id)
            {
                await OnSaved.InvokeAsync(dto);
                return;
            }
            _error = response.Status is 0 or 408 or 499 or 504 or (>= 200 and < 300) ?
                new("Response unavailable", "We could not confirm whether your changes were saved. Reload the news page to check before trying again. Your text has been kept.") :
                ApiErrorPresentation.Describe(response.Status, response.Body, "news");
            _validationErrors = ApiErrorPresentation.ValidationErrors(response.Body);
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException)
        {
            if (!_disposed) _error = new("Request interrupted", "We could not confirm whether your changes were saved. Reload the news page to check before trying again. Your text has been kept.");
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
