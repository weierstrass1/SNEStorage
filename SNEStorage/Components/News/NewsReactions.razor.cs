using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SNEStorage.Components.Shared;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorage.Components.News;

public partial class NewsReactions : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _key = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _lifetime = new();
    private IJSObjectReference? _module, _monitor;
    private IJSObjectReference? _sharedModule;
    private VerifiedAccountContent.BrowserAccount? _feedAccount;
    private DotNetObjectReference<NewsReactions>? _receiver;
    private NewsReadDTO? _snapshot;
    private NewsApiClient? _api;
    private string? _userId, _message;
    private int _likes, _dislikes, _revision;
    private LikeValues _vote = LikeValues.Remove;
    private bool _identityKnown, _saving, _refreshing, _needsRefresh, _disposed;

    [Parameter, EditorRequired] public NewsReadDTO Item { get; set; } = default!;
    [CascadingParameter(Name = "NewsFeedContext")] private NewsFeedContext? FeedContext { get; set; }
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private ILogger<NewsReactions> Logger { get; set; } = default!;
    private int Approval => (int)Math.Round(100.0 * _likes / (_likes + (long)_dislikes));
    private string VoteState => JsonSerializer.Serialize(new { mode = "reaction", context = $"news:{Item.Id}:{_userId}", likes = _likes, dislikes = _dislikes, vote = _vote.ToString(), decimals = 0 });

    protected override void OnParametersSet()
    {
        if (ReferenceEquals(_snapshot, Item)) return;
        _snapshot = Item;
        if (!_saving) ApplySnapshot(Item);
    }

    private void ApplySnapshot(NewsReadDTO item)
    {
        _likes = Math.Max(0, item.Likes);
        _dislikes = Math.Max(0, item.Dislikes);
        _vote = Enum.IsDefined(item.MyVote) ? item.MyVote : LikeValues.Remove;
        if (_identityKnown && _userId is null) _vote = LikeValues.Remove;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (FeedContext is not { } context) return;
        if (!ReferenceEquals(_sharedModule, context.Module))
        {
            _sharedModule = context.Module;
            _api = new NewsApiClient(context.Module, _key);
        }
        if (context.Account is { } account && !ReferenceEquals(_feedAccount, account))
        {
            _feedAccount = account;
            await UpdateAccount(account);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed || FeedContext is not null) return;
        try
        {
            var module = await JavaScript.InvokeAsync<IJSObjectReference>("import", _lifetime.Token, "./js/news.js?v=9");
            if (_disposed) { await module.DisposeAsync(); return; }
            _module = module;
            _api = new NewsApiClient(module, _key);
            _receiver = DotNetObjectReference.Create(this);
            _monitor = await module.InvokeAsync<IJSObjectReference>("watchAccount", _lifetime.Token, _receiver);
            if (_disposed && _monitor is not null)
            {
                await _monitor.InvokeVoidAsync("stop");
                await _monitor.DisposeAsync();
            }
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException)
        {
            Logger.LogDebug(exception, "News reaction controls could not be initialized.");
        }
    }

    [JSInvokable]
    public Task UpdateAccount(VerifiedAccountContent.BrowserAccount account) => InvokeAsync(async () =>
    {
        if (_disposed || account.Authenticated is null) return;
        string? userId = account.Authenticated == true && !string.IsNullOrWhiteSpace(account.UserId) ? account.UserId : null;
        bool changed = _identityKnown && userId != _userId;
        _identityKnown = true;
        _userId = userId;
        if (changed)
        {
            _revision++;
            _message = null;
            _vote = LikeValues.Remove;
            // A different account must not inherit the previous account's pressed button.
            await RefreshAsync();
        }
        else if (userId is null) _vote = LikeValues.Remove;
        if (!_disposed) StateHasChanged();
    });

    private async Task VoteAsync(LikeValues button)
    {
        if (_disposed || _saving || _refreshing || _needsRefresh || _api is null || _userId is null) return;
        var userId = _userId;
        var revision = _revision;
        var value = _vote == button ? LikeValues.Remove : button;
        var previous = (_likes, _dislikes, _vote);
        var confirmed = false;
        _saving = true;
        _message = null;
        ApplyVote(value);
        StateHasChanged();
        try
        {
            var response = await _api.VoteAsync(Item.Id, value, userId);
            if (_disposed || revision != _revision) return;
            bool alreadyRemoved = value == LikeValues.Remove && response.Status == 404 &&
                ApiErrorPresentation.Describe(response.Status, response.Body, "news").Code == "LikeNotFound";
            if (response.Status != 0 && !alreadyRemoved && (response.Status is < 200 or >= 300))
            {
                _message = ApiErrorPresentation.Describe(response.Status, response.Body, "news").Message;
                return;
            }
            confirmed = alreadyRemoved;
            if (response.Status is >= 200 and < 300)
            {
                var saved = JsonSerializer.Deserialize<LikeUsers>(response.Body, JsonOptions);
                confirmed = saved is { Id: > 0 } && saved.ScoreId == Item.ScoreId && saved.UserId == userId &&
                    (value == LikeValues.Remove || saved.Value == (value == LikeValues.Like));
            }
            if (confirmed) return;
            // Read back authoritative totals. Never repeat a write after a lost response.
            _needsRefresh = true;
            await RefreshAsync();
            confirmed = !_needsRefresh;
            if (!_disposed && revision == _revision && !_needsRefresh && _vote != value)
                _message = "We could not confirm your change. The current reactions are shown; you can try again.";
        }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception) when (exception is JSException or JsonException or OperationCanceledException)
        {
            Logger.LogDebug(exception, "A news reaction could not be confirmed.");
            if (!_disposed && revision == _revision)
            {
                _needsRefresh = true;
                _message = "We could not confirm your vote. Refresh reactions before trying again.";
            }
        }
        finally
        {
            if (!_disposed && revision == _revision)
            {
                if (!confirmed) (_likes, _dislikes, _vote) = previous;
                _saving = false;
                StateHasChanged();
            }
            else if (!_disposed) { _saving = false; StateHasChanged(); }
        }
    }

    private void ApplyVote(LikeValues value)
    {
        if (_vote == LikeValues.Like) _likes = Math.Max(0, _likes - 1);
        if (_vote == LikeValues.Dislike) _dislikes = Math.Max(0, _dislikes - 1);
        if (value == LikeValues.Like) _likes++;
        if (value == LikeValues.Dislike) _dislikes++;
        _vote = value;
    }

    private async Task RefreshAsync()
    {
        if (_disposed || _api is null) return;
        var revision = _revision;
        _refreshing = _needsRefresh = true;
        try
        {
            var response = await _api.ReadOneAsync(Item.Id, _lifetime.Token);
            if (_disposed || revision != _revision) return;
            if (response.Item is null)
            {
                _message = "Reactions could not be refreshed. Refresh reactions before voting again.";
                return;
            }
            ApplySnapshot(response.Item);
            _needsRefresh = false;
            _message = null;
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or JsonException or OperationCanceledException)
        {
            Logger.LogDebug(exception, "News reactions could not be refreshed.");
            if (!_disposed && revision == _revision)
                _message = "Reactions could not be refreshed. Refresh reactions before voting again.";
        }
        finally
        {
            if (!_disposed && revision == _revision) { _refreshing = false; StateHasChanged(); }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        try
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            // Ordinary feed cards own no JS handles and require no interop on exit.
            // Only an outstanding verification GET needs cancellation; vote POSTs continue.
            if (_module is null && _sharedModule is not null && _refreshing)
                await _sharedModule.InvokeVoidAsync("dispose", cleanup.Token, _key);
            if (_monitor is not null)
            {
                await _monitor.InvokeVoidAsync("stop", cleanup.Token);
                await _monitor.DisposeAsync();
            }
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("dispose", cleanup.Token, _key);
                await _module.InvokeVoidAsync("dispose", cleanup.Token, $"{_key}-vote");
                await _module.DisposeAsync();
            }
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException or ObjectDisposedException) { }
        finally { _receiver?.Dispose(); _lifetime.Dispose(); }
    }
}
