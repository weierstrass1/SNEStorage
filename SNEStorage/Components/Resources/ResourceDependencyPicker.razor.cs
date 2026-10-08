using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Resources;

public partial class ResourceDependencyPicker : IAsyncDisposable
{
    private const int MaxResults = 10;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<long, string> _names = [];
    private IJSObjectReference? _module;
    private CancellationTokenSource? _debounce;
    private List<SelectOptionDto> _matches = [];
    private string _search = string.Empty;
    private string? _error;
    private int _revision;
    private bool _loading;
    private bool _ready;
    private bool _disposed;
    [Parameter] public long[] Values { get; set; } = [];
    [Parameter] public EventCallback<long[]> ValuesChanged { get; set; }
    [Parameter] public IReadOnlyList<SelectOptionDto> InitialChoices { get; set; } = [];
    [Parameter] public long? ExcludedResourceId { get; set; }
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private ILogger<ResourceDependencyPicker> Logger { get; set; } = default!;

    private IEnumerable<SelectOptionDto> Suggestions => _matches.Where(item => !Values.Contains(item.Id) && item.Id != ExcludedResourceId);

    protected override void OnParametersSet()
    {
        foreach (var item in InitialChoices) _names[item.Id] = item.Name ?? $"Resource #{item.Id}";
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed) return;
        try
        {
            var module = await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/author-search.js");
            if (_disposed) { await module.DisposeAsync(); return; }
            _module = module;
            _ready = true;
        }
        catch (JSException exception)
        {
            Logger.LogWarning(exception, "Resource dependency search could not be initialized.");
            _error = "Resource search could not be loaded. Please reload the page.";
        }
        if (!_disposed) StateHasChanged();
    }

    private async Task SearchChangedAsync(ChangeEventArgs args)
    {
        _search = args.Value?.ToString() ?? string.Empty;
        await SearchAsync(debounce: true);
    }
    private Task RetryAsync() => SearchAsync(debounce: false);
    private Task RemoveAsync(long id) => ValuesChanged.InvokeAsync(Values.Where(value => value != id).ToArray());
    private async Task AddAsync(SelectOptionDto item)
    {
        var id = item.Id;
        if (Values.Contains(id) || id == ExcludedResourceId) return;
        _names[id] = item.Name ?? $"Resource #{id}";
        _debounce?.Cancel();
        ++_revision;
        _search = string.Empty;
        _matches = [];
        _loading = false;
        await ValuesChanged.InvokeAsync(Values.Append(id).ToArray());
    }

    private async Task SearchAsync(bool debounce)
    {
        if (_module is null || _disposed) return;
        _debounce?.Cancel();
        _debounce?.Dispose();
        _debounce = new CancellationTokenSource();
        var token = _debounce.Token;
        var revision = ++_revision;
        var term = _search.Trim();
        _matches = [];
        _error = null;
        _loading = term.Length >= 2;
        if (!_loading) return;
        try
        {
            if (debounce) await Task.Delay(TimeSpan.FromMilliseconds(300), token);
            if (token.IsCancellationRequested || _disposed) return;
            var response = await _module.InvokeAsync<SearchResponse>("searchResources", term, MaxResults);
            if (_disposed || revision != _revision) return;
            if (response.Status != 200 || !response.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                _error = ApiErrorPresentation.Describe(response.Status, response.Body, "resource").Message;
                return;
            }
            _matches = (JsonSerializer.Deserialize<List<SelectOptionDto>>(response.Body, JsonOptions) ?? [])
                .Where(item => item.Id > 0 && !string.IsNullOrWhiteSpace(item.Name))
                .DistinctBy(item => item.Id).Take(MaxResults).ToList();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception exception) when (exception is JSException or JsonException)
        {
            if (!_disposed && revision == _revision)
            {
                Logger.LogWarning(exception, "Resource dependency search failed.");
                _error = "Resources could not be loaded. Please try again.";
            }
        }
        finally { if (!_disposed && revision == _revision) _loading = false; }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _debounce?.Cancel();
        _debounce?.Dispose();
        if (_module is null) return;
        try { await _module.DisposeAsync(); }
        catch (JSDisconnectedException) { }
    }
    private sealed class SearchResponse
    {
        public int Status { get; set; }
        public string Body { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }
}
