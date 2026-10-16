using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Resources;

public partial class SearchableVideogamePicker : IAsyncDisposable
{
    private const int MaxResults = 10;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _searchKey = Guid.NewGuid().ToString("N");
    private IJSObjectReference? _module;
    private CancellationTokenSource? _pending;
    private List<SelectOptionDto> _defaults = [];
    private List<SelectOptionDto> _matches = [];
    private SelectOptionDto? _selected;
    private string _search = string.Empty;
    private string? _error;
    private int _revision;
    private bool _loading = true;
    private bool _ready;
    private bool _disposed;

    [Parameter]
    public long? Value { get; set; }

    [Parameter]
    public SelectOptionDto? InitialSelection { get; set; }

    [Parameter]
    public EventCallback<long?> ValueChanged { get; set; }

    [Parameter] public bool Required { get; set; } = true;
    [Parameter] public bool AutoSelectAny { get; set; } = true;
    [Parameter] public string Placeholder { get; set; } = "Choose a game";
    [Parameter] public EventCallback<SelectOptionDto> SelectionChanged { get; set; }

    [Inject]
    private IJSRuntime JavaScript { get; set; } = default!;

    [Inject]
    private ILogger<SearchableVideogamePicker> Logger { get; set; } = default!;

    protected string SelectedName => _selected?.Name ?? Placeholder;

    protected override void OnParametersSet()
    {
        if (_selected?.Id != Value)
        {
            _selected = _matches.Concat(_defaults).FirstOrDefault(game => game.Id == Value)
                ?? (InitialSelection?.Id == Value ? InitialSelection : null);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed)
        {
            return;
        }

        try
        {
            var module = await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/videogame-search.js");
            if (_disposed)
            {
                await module.DisposeAsync();
                return;
            }

            _module = module;
            await SearchAsync(debounce: false);
        }
        catch (JSException exception)
        {
            Logger.LogWarning(exception, "The video game search could not be initialized.");
            _loading = false;
            _error = "The game selector could not be loaded. Please reload the page.";
        }

        if (!_disposed)
        {
            _ready = _module is not null;
            StateHasChanged();
        }
    }

    protected Task SearchChangedAsync() => SearchAsync(debounce: true);

    protected Task RetryAsync() => SearchAsync(debounce: false);

    protected async Task SelectAsync(SelectOptionDto game)
    {
        _selected = game;
        await ValueChanged.InvokeAsync(game.Id);
        await SelectionChanged.InvokeAsync(game);
    }

    private async Task SearchAsync(bool debounce)
    {
        if (_module is null || _disposed)
        {
            return;
        }

        _pending?.Cancel();
        _pending?.Dispose();
        _pending = new CancellationTokenSource();
        var token = _pending.Token;
        var revision = ++_revision;
        var term = _search.Trim();
        _loading = true;
        _error = null;
        _matches = [];

        try
        {
            await _module.InvokeVoidAsync("cancelSearch", _searchKey);
            if (token.IsCancellationRequested || _disposed)
            {
                return;
            }

            if (term.Length == 0 && _defaults.Count > 0)
            {
                _matches = _defaults;
                return;
            }

            if (term.Length == 1)
            {
                return;
            }

            if (debounce && term.Length >= 2)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(300), token);
            }

            var response = await _module.InvokeAsync<SearchResponse>(
                "searchVideogames", token, _searchKey, term, MaxResults);
            if (token.IsCancellationRequested || revision != _revision || _disposed || response.Cancelled)
            {
                return;
            }

            if (response.Status != 200 || !response.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                _error = ApiErrorPresentation.Describe(response.Status, response.Body, "search").Message;
                return;
            }

            // The controller returns Videogame entities; only Id and Name are needed for the selector.
            var choices = JsonSerializer.Deserialize<List<SelectOptionDto>>(response.Body, JsonOptions) ?? [];
            if (term.Length == 0)
            {
                _defaults = [.. choices.Where(game => game.Id is 1 or 2).OrderBy(game => game.Id)];
                _matches = _defaults;
                if (AutoSelectAny && Value is null && _defaults.FirstOrDefault(game => game.Id == 1) is { } anyGame)
                {
                    await SelectAsync(anyGame);
                }
            }
            else
            {
                _matches = choices;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // A new input or leaving the page supersedes this request.
        }
        catch (Exception exception) when (exception is JSException or JsonException)
        {
            if (!token.IsCancellationRequested && revision == _revision && !_disposed)
            {
                Logger.LogWarning(exception, "Video game search failed.");
                _error = "Games could not be loaded. Please try again.";
            }
        }
        finally
        {
            if (revision == _revision && !_disposed)
            {
                _loading = false;
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _pending?.Cancel();
        _pending?.Dispose();
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("cancelSearch", _searchKey);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The browser connection has already closed.
            }
        }
    }

    private sealed class SearchResponse
    {
        public int Status { get; set; }
        public string Body { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
        public bool Cancelled { get; set; }
    }
}
