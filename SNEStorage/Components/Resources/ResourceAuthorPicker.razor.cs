using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Resources;

public partial class ResourceAuthorPicker : IAsyncDisposable
{
    private const int MaxResults = 10;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly Dictionary<string, string> _usernames = new(StringComparer.Ordinal);
    private IJSObjectReference? _module;
    private CancellationTokenSource? _debounce;
    private List<AuthorChoice> _matches = [];
    private string _search = string.Empty;
    private string? _error;
    private int _revision;
    private bool _loading;
    private bool _ready;
    private bool _disposed;

    [Parameter] public bool External { get; set; }
    [Parameter] public string[] Values { get; set; } = [];
    [Parameter] public EventCallback<string[]> ValuesChanged { get; set; }
    [Parameter] public string? ExcludedUserId { get; set; }
    [Parameter] public IReadOnlyDictionary<string, string> InitialNames { get; set; } = new Dictionary<string, string>();
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private ILogger<ResourceAuthorPicker> Logger { get; set; } = default!;

    private string InputId => External ? "resource-external-author-search" : "resource-user-author-search";
    private string FieldName => External ? "ExternalAuthors" : "AuthorsIDs";
    private StringComparer Comparer => External ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;
    private bool CanAddName => !string.IsNullOrWhiteSpace(_search) && !Values.Contains(_search.Trim(), Comparer);
    private IEnumerable<AuthorChoice> Suggestions => _matches
        .Where(choice => !Values.Contains(choice.Value, Comparer) && (External || choice.Value != ExcludedUserId));

    protected override async Task OnParametersSetAsync()
    {
        foreach (var (id, name) in InitialNames) _usernames[id] = name;
        // IAmAuthor already credits the submitter; do not send that account a second time.
        if (!External && ExcludedUserId is not null && Values.Contains(ExcludedUserId, Comparer))
        {
            await ValuesChanged.InvokeAsync([.. Values.Where(value => value != ExcludedUserId)]);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed) return;
        try
        {
            var module = await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/author-search.js?v=2");
            if (_disposed)
            {
                await module.DisposeAsync();
                return;
            }
            _module = module;
            _ready = true;
        }
        catch (JSException exception)
        {
            Logger.LogWarning(exception, "The author selector could not be initialized.");
            _error = "The author selector could not be loaded. Please reload the page.";
        }
        if (!_disposed) StateHasChanged();
    }

    private string DisplayName(string value) => External ? value : _usernames.GetValueOrDefault(value, "Selected user");

    // Binding tracks the current browser value before async searches can re-render.
    private Task SearchChangedAsync() => SearchAsync(debounce: true);

    private Task RetryAsync() => SearchAsync(debounce: false);

    private Task KeyDownAsync(KeyboardEventArgs args) => External && args.Key == "Enter"
        ? AddPendingAsync() : Task.CompletedTask;

    private Task AddPendingAsync() => CanAddName
        ? AddAsync(new AuthorChoice(_search.Trim(), _search.Trim())) : Task.CompletedTask;

    private async Task AddAsync(AuthorChoice choice)
    {
        if (choice.Value.Length > (External ? TextLimits.EXTERNAL_AUTHOR : TextLimits.USER_ID))
        {
            _error = "This author name is too long.";
            return;
        }
        if (Values.Contains(choice.Value, Comparer) || (!External && choice.Value == ExcludedUserId)) return;
        if (!External) _usernames[choice.Value] = choice.Name;
        ClearSearch();
        await ValuesChanged.InvokeAsync([.. Values, choice.Value]);
    }

    private Task RemoveAsync(string value) => ValuesChanged.InvokeAsync(
        [.. Values.Where(selected => !Comparer.Equals(selected, value))]);

    private void ClearSearch()
    {
        _debounce?.Cancel();
        ++_revision;
        _search = string.Empty;
        _matches = [];
        _loading = false;
        _error = null;
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
        _error = null;
        _matches = [];
        _loading = term.Length >= 2;
        if (!_loading) return;

        try
        {
            if (debounce) await Task.Delay(TimeSpan.FromMilliseconds(300), token);
            if (token.IsCancellationRequested || _disposed) return;

            // Cancel only the typing delay. Completed HTTP responses are ignored if superseded.
            var response = await _module.InvokeAsync<SearchResponse>(
                External ? "searchExternalAuthors" : "searchUsers", term, MaxResults);
            if (_disposed || revision != _revision) return;
            if (response.Status != 200 || !response.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                _error = ApiErrorPresentation.Describe(response.Status, response.Body, "search").Message;
                return;
            }

            if (External)
            {
                var names = JsonSerializer.Deserialize<List<string?>>(response.Body, JsonOptions) ?? [];
                _matches = [.. names.Where(name => !string.IsNullOrWhiteSpace(name)).Select(name => name!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxResults)
                    .Select(name => new AuthorChoice(name, name))];
            }
            else
            {
                // Consume only the public account fields needed to choose an author.
                var users = JsonSerializer.Deserialize<List<UserChoice>>(response.Body, JsonOptions) ?? [];
                _matches = [.. users.Where(user => !string.IsNullOrWhiteSpace(user.Id) && !string.IsNullOrWhiteSpace(user.Username))
                    .DistinctBy(user => user.Id, StringComparer.Ordinal).Take(MaxResults)
                    .Select(user => new AuthorChoice(user.Id, user.Username))];
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // Another keystroke superseded the typing delay.
        }
        catch (Exception exception) when (exception is JSException or JsonException)
        {
            if (!_disposed && revision == _revision)
            {
                Logger.LogWarning(exception, "Author search failed for {FieldName}.", FieldName);
                _error = "Authors could not be loaded. Please try again.";
            }
        }
        finally
        {
            if (!_disposed && revision == _revision) _loading = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _debounce?.Cancel();
        _debounce?.Dispose();
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); }
            catch (JSDisconnectedException) { }
        }
    }

    private sealed record AuthorChoice(string Value, string Name);
    private sealed class UserChoice
    {
        public string Id { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
    }
    private sealed class SearchResponse
    {
        public int Status { get; set; }
        public string Body { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }
}
