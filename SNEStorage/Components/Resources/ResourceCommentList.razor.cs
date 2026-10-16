using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SNEStorage.DTOs;
using SNEStorage.Services;

namespace SNEStorage.Components.Resources;

public partial class ResourceCommentList : IAsyncDisposable
{
    private const int PageSize = 20;
    private readonly Dictionary<long, CommentActionState> _actions = [];
    private List<CommentReadDto> _comments = [];
    private readonly List<CommentReadDto> _recentComments = [];
    private CommentApiClient? _api;
    private string? _contextKey;
    private string? _error;
    private string? _actionMessage;
    private CommentSorting _sort = CommentSorting.RecentFirst;
    private int _page;
    private int _retryPage;
    private int? _retryThroughPage;
    private bool _retryAppend;
    private int _lastRevision;
    private int _generation;
    private int _contextVersion;
    private bool _pending;
    private bool _loading = true;
    private bool _loaded;
    private bool _hasNext;
    private bool _busy;
    private bool _disposed;

    [Parameter] public long ResourceId { get; set; }
    [Parameter] public long? ParentCommentId { get; set; }
    [Parameter] public bool IsAuthenticated { get; set; }
    [Parameter] public string? CurrentUserId { get; set; }
    [Parameter] public bool CanModerate { get; set; }
    [Parameter] public int Revision { get; set; }
    [Parameter] public CommentReadDto? PublishedComment { get; set; }
    [Parameter] public IReadOnlyList<long> Ancestors { get; set; } = [];
    [Parameter] public EventCallback OnEmpty { get; set; }
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private ILogger<ResourceCommentList> Logger { get; set; } = default!;

    protected IReadOnlyList<CommentReadDto> VisibleComments => [.. _recentComments.Concat(_comments)
        .DistinctBy(c => c.Id).Where(c => !Ancestors.Contains(c.Id) &&
            (!c.Deleted || c.HasReplies))];
    protected bool IsRecentlyPosted(long id) => _recentComments.Any(c => c.Id == id);

    protected override void OnParametersSet()
    {
        var key = $"{ResourceId}:{ParentCommentId}:{CurrentUserId}:{IsAuthenticated}:{CanModerate}";
        if (_contextKey != key)
        {
            _contextKey = key;
            _contextVersion++;
            _generation++;
            _comments = [];
            _recentComments.Clear();
            _actions.Clear();
            _scrollTarget = null;
            _busy = false;
            _page = 0;
            _loaded = false;
            _actionMessage = null;
            _error = null;
            _pending = true;
        }
        if (_lastRevision != Revision)
        {
            if (PublishedComment is { Id: > 0 } published && published.UserId == CurrentUserId)
            {
                _recentComments.RemoveAll(c => c.Id == published.Id);
                _comments.RemoveAll(c => c.Id == published.Id);
                _recentComments.Insert(0, published);
                _scrollTarget = published.Id;
            }
            else _pending = true;
        }
        _lastRevision = Revision;
        if (_pending) _loading = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed) return;
        if (_pending && !_busy)
        {
            _pending = false;
            _api ??= new CommentApiClient(JavaScript, Logger);
            await LoadPageAsync(0);
        }
        await UpdateFeedAsync();
    }

    private async Task LoadPageAsync(int page, bool append = false, int? throughPage = null)
    {
        if (_disposed || _api is null) return;
        var generation = ++_generation;
        _retryPage = page;
        _retryAppend = append;
        _retryThroughPage = throughPage;
        _loading = true;
        _error = null;
        StateHasChanged();
        List<CommentReadDto> fetched = [];
        var lastPage = page;
        var hasNext = false;
        for (var currentPage = page; currentPage <= (throughPage ?? page); currentPage++)
        {
            var result = await _api.GetAsync(ResourceId, ParentCommentId, currentPage, PageSize, _sort);
            if (_disposed || generation != _generation) return;
            if (!result.Success)
            {
                _loading = false;
                _error = result.Message ?? "The request was interrupted. Try loading the comments again.";
                StateHasChanged();
                return;
            }
            fetched.AddRange(result.Value!);
            lastPage = currentPage;
            hasNext = result.Value!.Count == PageSize;
            if (!hasNext) break;
        }
        // Keep the existing feed visible while requesting the next page, and merge by ID.
        _comments = [.. (append ? _comments.Concat(fetched) : fetched).DistinctBy(c => c.Id)];
        _recentComments.RemoveAll(c => _comments.Any(loaded => loaded.Id == c.Id));
        _page = lastPage;
        _hasNext = hasNext;
        _loaded = true;
        _loading = false;
        StateHasChanged();
        if (!_hasNext && _comments.Count == 0 && _recentComments.Count == 0 && ParentCommentId is not null)
            await OnEmpty.InvokeAsync();
    }

    protected async Task ChangeSortAsync(ChangeEventArgs args)
    {
        if (_busy || _loading || !Enum.TryParse<CommentSorting>(args.Value?.ToString(), out var sort) || !Enum.IsDefined(sort)) return;
        _sort = sort;
        _comments = [];
        _recentComments.Clear();
        _actions.Clear();
        _scrollTarget = null;
        _page = 0;
        _loaded = false;
        _actionMessage = null;
        await LoadPageAsync(0);
    }

    protected Task LoadMoreAsync() => !_loading && !_busy && !_pending && _hasNext ? LoadPageAsync(_page + 1, append: true) : Task.CompletedTask;
    protected Task RetryAsync() => !_loading && !_busy
        ? LoadPageAsync(_retryPage, _retryAppend, _retryThroughPage) : Task.CompletedTask;
    protected Task RefreshAsync() => !_loading && !_busy ? LoadPageAsync(0, throughPage: _page) : Task.CompletedTask;
    protected string? ActionMessage(long id) => _actions.GetValueOrDefault(id)?.Message;
    protected int ActionRevision(long id) => _actions.GetValueOrDefault(id)?.Revision ?? 0;
    protected int ReplyRevision(long id) => _actions.GetValueOrDefault(id)?.ReplyRevision ?? 0;
    protected CommentReadDto? PublishedReply(long id) => _actions.GetValueOrDefault(id)?.PublishedReply;

    private void ReplaceComment(CommentReadDto comment)
    {
        var index = _comments.FindIndex(c => c.Id == comment.Id);
        if (index >= 0) _comments[index] = comment;
        index = _recentComments.FindIndex(c => c.Id == comment.Id);
        if (index >= 0) _recentComments[index] = comment;
    }

    protected Task HandleRepliesEmptyAsync(long commentId)
    {
        // An exhausted, empty replies GET confirms that this displayed parent has no children.
        var comment = _comments.Concat(_recentComments).FirstOrDefault(c => c.Id == commentId);
        if (comment is not null) ReplaceComment(CommentPresentation.Update(comment, hasReplies: false));
        if (_actions.TryGetValue(commentId, out var state)) state.PublishedReply = null;
        return Task.CompletedTask;
    }

    protected async Task HandleVoteAsync(CommentVoteRequest request)
    {
        if (_disposed || _busy || _loading || _api is null || !IsAuthenticated || string.IsNullOrEmpty(CurrentUserId)) return;
        var comment = VisibleComments.FirstOrDefault(c => c.Id == request.CommentId);
        if (comment is null || comment.Deleted || comment.UserId == CurrentUserId) return;
        var selected = request.Positive ? LikeValues.Like : LikeValues.Dislike;
        var value = comment.MyVote == selected ? LikeValues.Remove : selected;
        var context = _contextVersion;
        var userId = CurrentUserId;
        if (!_actions.TryGetValue(comment.Id, out var state)) _actions[comment.Id] = state = new();
        _busy = true;
        state.Message = null;
        ReplaceComment(CommentPresentation.WithVote(comment, value));
        StateHasChanged();
        try
        {
            var result = await _api.VoteAsync(comment, value, userId);
            if (_disposed || context != _contextVersion) return;
            if (!result.Success)
            {
                ReplaceComment(comment);
                state.Message = result.Message ?? "Your vote could not be saved. Please try again.";
                return;
            }
            // Reconcile the optimistic state using the original counts, without adding twice.
            var confirmed = value == LikeValues.Remove || result.Value is null ? LikeValues.Remove
                : result.Value.Value ? LikeValues.Like : LikeValues.Dislike;
            ReplaceComment(CommentPresentation.WithVote(comment, confirmed));
        }
        finally
        {
            if (!_disposed && context == _contextVersion) { _busy = false; StateHasChanged(); }
        }
    }

    protected async Task HandleActionAsync(CommentActionRequest request)
    {
        if (_disposed || _busy || _loading || _api is null || !IsAuthenticated) return;
        var comment = VisibleComments.FirstOrDefault(c => c.Id == request.CommentId);
        if (comment is null || comment.Deleted || request.Kind != CommentActionKind.Reply &&
            !CanModerate && comment.UserId != CurrentUserId) return;
        if (request.Kind != CommentActionKind.Delete && string.IsNullOrWhiteSpace(request.Text)) return;
        var context = _contextVersion;
        if (!_actions.TryGetValue(comment.Id, out var state)) _actions[comment.Id] = state = new();
        _busy = true;
        state.Message = null;
        _actionMessage = null;
        try
        {
            var result = await _api.SubmitAsync(request, CurrentUserId);
            if (_disposed || context != _contextVersion) return;
            if (!result.Success) { state.Message = result.Message; return; }
            state.Revision++;
            if (request.Kind == CommentActionKind.Reply)
            {
                ReplaceComment(CommentPresentation.Update(comment, hasReplies: true));
                state.ReplyRevision++;
                state.PublishedReply = result.Value?.ChildComment is { } child
                    ? CommentPresentation.FromCreatedComment(child) : null;
                state.Message = "Your reply has been posted.";
            }
            else
            {
                _actionMessage = request.Kind == CommentActionKind.Edit ? "Your comment has been updated." : "The comment has been deleted.";
                // Keep a just-posted comment accurate even when the selected sort places it on a later page.
                if (request.Kind == CommentActionKind.Edit)
                    ReplaceComment(CommentPresentation.Update(comment, text: request.Text!));
                else
                {
                    if (comment.HasReplies)
                        ReplaceComment(CommentPresentation.Update(comment, deleted: true, text: string.Empty));
                    else
                    {
                        _recentComments.RemoveAll(c => c.Id == comment.Id);
                        _comments.RemoveAll(c => c.Id == comment.Id);
                    }
                }
                // The GET decides whether deletion removed the row or left a placeholder.
                await LoadPageAsync(0, throughPage: _page);
            }
        }
        finally
        {
            if (!_disposed && context == _contextVersion) _busy = false;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _generation++;
        _contextVersion++;
        await DisposeFeedAsync();
        if (_api is not null) await _api.DisposeAsync();
    }
}
