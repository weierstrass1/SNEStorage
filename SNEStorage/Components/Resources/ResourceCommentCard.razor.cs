using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using SNEStorage.DTOs;
using SNEStorage.Services;

namespace SNEStorage.Components.Resources;

public partial class ResourceCommentCard
{
    private readonly CommentDraft _reply = new();
    private readonly CommentDraft _edit = new();
    private long _previousCommentId;
    private CommentReadDto? _previousComment;
    private bool? _hasRepliesOverride;
    private int _previousActionRevision;
    private int _previousReplyRevision;
    private bool _showReplies;
    private bool _replying;
    private bool _editing;
    private bool _confirmDelete;
    private CommentReadDto? _publishedReply;
    private static readonly Regex HexColor = new("\\A#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})\\z", RegexOptions.Compiled);
    [Parameter, EditorRequired] public CommentReadDto Comment { get; set; } = default!;
    [Parameter] public long ResourceId { get; set; }
    [Parameter] public IReadOnlyList<long> Ancestors { get; set; } = [];
    [Parameter] public bool IsAuthenticated { get; set; }
    [Parameter] public string? CurrentUserId { get; set; }
    [Parameter] public bool CanModerate { get; set; }
    [Parameter] public bool Saving { get; set; }
    [Parameter] public string? Message { get; set; }
    [Parameter] public int ActionRevision { get; set; }
    [Parameter] public int ReplyRevision { get; set; }
    [Parameter] public CommentReadDto? PublishedReply { get; set; }
    [Parameter] public bool JustPosted { get; set; }
    [Parameter] public EventCallback<CommentActionRequest> OnAction { get; set; }
    [Parameter] public EventCallback<CommentVoteRequest> OnVote { get; set; }
    [Parameter] public EventCallback<long> OnRepliesEmpty { get; set; }
    protected bool CanManage => IsAuthenticated && !Comment.Deleted &&
        (CanModerate || !string.IsNullOrEmpty(CurrentUserId) && Comment.UserId == CurrentUserId);
    protected bool CanReply => IsAuthenticated && !Comment.Deleted && OnAction.HasDelegate;
    protected bool CanVote => IsAuthenticated && !string.IsNullOrEmpty(CurrentUserId) &&
        Comment.UserId != CurrentUserId && !Comment.Deleted && OnVote.HasDelegate;
    protected bool HasReplies => _hasRepliesOverride ?? Comment.HasReplies;
    protected int Likes => Comment.Likes;
    protected int Dislikes => Comment.Dislikes;
    protected string VoteState => JsonSerializer.Serialize(new { mode = "reaction", context = $"comment:{Comment.Id}:{CurrentUserId}", likes = Likes, dislikes = Dislikes, vote = Comment.MyVote.ToString(), decimals = 1 });
    protected string Approval => (100d * Likes / (Likes + (long)Dislikes)).ToString("0.#", CultureInfo.GetCultureInfo("en-US"));
    protected bool? MyVote => IsAuthenticated && !string.IsNullOrEmpty(CurrentUserId)
        ? Comment.MyVote switch { LikeValues.Like => true, LikeValues.Dislike => false, _ => null } : null;
    protected string Username => string.IsNullOrWhiteSpace(Comment.UserSummary?.Username) ? "Community member" : Comment.UserSummary.Username;
    protected string UsernameColor => Comment.UserSummary?.UsernameColor is { } color && HexColor.IsMatch(color) ? color : "#e6d2fb";
    protected string? ProfileUrl => string.IsNullOrWhiteSpace(Comment.UserSummary?.Username) ? null : $"/Profile/{Uri.EscapeDataString(Comment.UserSummary.Id)}";
    protected string AvatarUrl => PublicFileUrl.Resolve(Comment.UserSummary?.Avatar?.URL, "/images/caja.png");
    protected IReadOnlyList<long> NextAncestors => [.. Ancestors, Comment.Id];
    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_previousComment, Comment))
            _hasRepliesOverride = null;
        if (_previousCommentId != Comment.Id)
        {
            _showReplies = false;
            _publishedReply = null;
            CloseEditor();
        }
        if (_previousActionRevision != ActionRevision)
            CloseEditor();
        if (Comment.Deleted) CloseEditor();
        if (_previousReplyRevision != ReplyRevision)
        {
            _hasRepliesOverride = true;
            _showReplies = true;
            _publishedReply = PublishedReply;
        }
        _previousCommentId = Comment.Id;
        _previousActionRevision = ActionRevision;
        _previousReplyRevision = ReplyRevision;
        _previousComment = Comment;
    }
    protected void ToggleReplies() { _showReplies = !_showReplies; if (!_showReplies) _publishedReply = null; }
    protected async Task RepliesEmpty()
    {
        _hasRepliesOverride = false;
        _showReplies = false;
        _publishedReply = null;
        await OnRepliesEmpty.InvokeAsync(Comment.Id);
    }
    protected Task LikeAsync() => CanVote && !Saving
        ? OnVote.InvokeAsync(new(Comment.Id, true)) : Task.CompletedTask;
    protected Task DislikeAsync() => CanVote && !Saving
        ? OnVote.InvokeAsync(new(Comment.Id, false)) : Task.CompletedTask;
    protected void BeginReply() { if (!CanReply || Saving || _replying) return; CloseEditor(); _replying = true; }
    protected void BeginEdit() { CloseEditor(); _edit.Text = Comment.Text; _editing = true; }
    protected void BeginDelete() { CloseEditor(); _confirmDelete = true; }
    protected void CloseEditor()
    {
        _replying = _editing = _confirmDelete = false;
        _reply.Text = _edit.Text = string.Empty;
    }
    protected void ReplyChanged(ChangeEventArgs args) => _reply.Text = args.Value?.ToString() ?? string.Empty;
    protected void EditChanged(ChangeEventArgs args) => _edit.Text = args.Value?.ToString() ?? string.Empty;
    protected Task ReplyAsync() => CanReply && !Saving && !string.IsNullOrWhiteSpace(_reply.Text)
        ? OnAction.InvokeAsync(new(Comment.Id, CommentActionKind.Reply, _reply.Text.Trim())) : Task.CompletedTask;
    protected Task EditAsync() => CanManage && !Saving && !string.IsNullOrWhiteSpace(_edit.Text)
        ? OnAction.InvokeAsync(new(Comment.Id, CommentActionKind.Edit, _edit.Text.Trim())) : Task.CompletedTask;
    protected Task DeleteAsync() => CanManage && !Saving
        ? OnAction.InvokeAsync(new(Comment.Id, CommentActionKind.Delete)) : Task.CompletedTask;
    protected static string FormatDate(DateTime date) => date.ToString("MMM d, yyyy · HH:mm 'UTC'", CultureInfo.GetCultureInfo("en-US"));
    private sealed class CommentDraft { public string Text { get; set; } = string.Empty; }
}
