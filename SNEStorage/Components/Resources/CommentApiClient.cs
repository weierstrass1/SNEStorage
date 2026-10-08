using System.Text.Json;
using Microsoft.JSInterop;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorage.Components.Resources;

// Browser transport to CommentController. No database or business service access.
internal sealed class CommentApiClient(IJSRuntime javascript, ILogger logger) : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _requestKey = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _lifetime = new();
    private IJSObjectReference? _module;
    private bool _disposed;

    public async Task<CommentApiResult<List<Comment>>> GetAsync(long resourceId, long? parentId,
        int page, int pageSize, CommentSorting sort)
    {
        var response = await CallAsync("getComments", resourceId, parentId, page, pageSize, sort.ToString(), _requestKey);
        if (response.Cancelled) return new(false, Cancelled: true);
        if (response.Status != 200)
            return new(false, Message: ApiErrorPresentation.Describe(response.Status, response.Body, "comment").Message);
        try
        {
            var comments = JsonSerializer.Deserialize<List<Comment>>(response.Body ?? "", JsonOptions);
            return comments is not null && comments.All(comment => comment.Id > 0)
                ? new(true, comments)
                : new(false, Message: "The comment list could not be read. Refresh the comments to try again.");
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "The comments response could not be read.");
            return new(false, Message: "The comment list could not be read. Refresh the comments to try again.");
        }
    }

    public async Task<CommentApiResult<LikeUsers>> VoteAsync(Comment comment, LikeValues value, string userId)
    {
        var response = await CallAsync("submitCommentVote", comment.Id, value.ToString(), $"{_requestKey}-action");
        const string uncertain = "We could not confirm your vote. Refresh the comments before trying again.";
        if (response.Cancelled) return new(false, Message: uncertain);
        if (response.Status == 0)
        {
            // A lost response may follow a committed write. Verify it without repeating the mutation.
            var current = await GetVoteAsync(comment, userId);
            if (current.Success && (value == LikeValues.Remove ? current.Value is null :
                current.Value?.Value == (value == LikeValues.Like)))
                return current;
            return new(false, Message: uncertain);
        }
        // A vote removed in another tab already satisfies the requested removal.
        if (value == LikeValues.Remove && response.Status == 404 &&
            ApiErrorPresentation.Describe(response.Status, response.Body, "comment").Code == nameof(CommentServiceResult.LikeNotFound))
            return new(true);
        if (response.Status is < 200 or >= 300)
            return new(false, Message: ApiErrorPresentation.Describe(response.Status, response.Body, "comment").Message);
        try
        {
            var vote = JsonSerializer.Deserialize<LikeUsers>(response.Body ?? "", JsonOptions);
            return vote is { Id: > 0 } && vote.ScoreId == comment.ScoreId && vote.UserId == userId &&
                (value == LikeValues.Remove || vote.Value == (value == LikeValues.Like))
                ? new(true, vote) : new(false, Message: uncertain);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "A comment vote response could not be read.");
            return new(false, Message: uncertain);
        }
    }

    private async Task<CommentApiResult<LikeUsers>> GetVoteAsync(Comment comment, string userId)
    {
        var response = await CallAsync("getCommentVote", comment.Id, _requestKey);
        if (response.Cancelled) return new(false, Cancelled: true);
        if (response.Status == 404 &&
            ApiErrorPresentation.Describe(response.Status, response.Body, "comment").Code == nameof(CommentServiceResult.LikeNotFound))
            return new(true);
        if (response.Status != 200) return new(false);
        try
        {
            var vote = JsonSerializer.Deserialize<LikeUsers>(response.Body ?? "", JsonOptions);
            return vote is { Id: > 0 } && vote.ScoreId == comment.ScoreId && vote.UserId == userId
                ? new(true, vote) : new(false);
        }
        catch (JsonException exception)
        {
            logger.LogDebug(exception, "The current comment vote could not be verified.");
            return new(false);
        }
    }

    public async Task<CommentApiResult<CommentComment>> SubmitAsync(CommentActionRequest request, string? userId)
    {
        var response = await CallAsync("submitCommentAction", request.CommentId, request.Kind.ToString(), request.Text, $"{_requestKey}-action");
        const string uncertain = "We could not confirm the change. Refresh the comments before trying again; your text has been kept.";
        if (response.Cancelled || response.Status == 0) return new(false, Message: uncertain);
        if (response.Status is < 200 or >= 300)
            return new(false, Message: ApiErrorPresentation.Describe(response.Status, response.Body, "comment").Message);
        try
        {
            if (request.Kind == CommentActionKind.Reply)
            {
                var saved = JsonSerializer.Deserialize<CommentComment>(response.Body ?? "", JsonOptions);
                return saved is { Id: > 0, ChildComment: { Id: > 0 } child } &&
                    saved.ParentCommentId == request.CommentId && child.UserId == userId
                    ? new(true, saved) : new(false, Message: uncertain);
            }
            using var document = JsonDocument.Parse(response.Body ?? "");
            var value = document.RootElement;
            var success = value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number == (int)CommentServiceResult.Success ||
                value.ValueKind == JsonValueKind.String && string.Equals(value.GetString(), nameof(CommentServiceResult.Success), StringComparison.OrdinalIgnoreCase);
            return success ? new(true) : new(false, Message: uncertain);
        }
        catch (JsonException exception)
        {
            logger.LogWarning(exception, "A comment mutation response could not be read.");
            return new(false, Message: uncertain);
        }
    }

    private async Task<BrowserResponse> CallAsync(string function, params object?[] arguments)
    {
        if (_disposed) return new() { Cancelled = true };
        try
        {
            _module ??= await javascript.InvokeAsync<IJSObjectReference>("import", _lifetime.Token, "./js/comment-api.js?v=4");
            if (_disposed) return new() { Cancelled = true };
            var json = await _module.InvokeAsync<string>(function, _lifetime.Token, arguments);
            return JsonSerializer.Deserialize<BrowserResponse>(json, JsonOptions) ?? new() { Status = 502 };
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or JsonException or OperationCanceledException)
        {
            logger.LogDebug(exception, "Comment request {Function} was interrupted or unavailable.", function);
            return new() { Cancelled = _disposed || exception is JSDisconnectedException or OperationCanceledException };
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        if (_module is not null)
        {
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _module.InvokeVoidAsync("cancelCommentRequests", cleanup.Token, _requestKey);
                await _module.DisposeAsync();
            }
            catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
        }
        _lifetime.Dispose();
    }

    private sealed class BrowserResponse
    {
        public int Status { get; set; }
        public string? Body { get; set; }
        public bool Cancelled { get; set; }
    }
}

internal sealed record CommentApiResult<T>(bool Success, T? Value = null, string? Message = null, bool Cancelled = false) where T : class;
