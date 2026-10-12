using Microsoft.AspNetCore.Components;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Resources;

public partial class ResourceCommentsPanel
{
    private readonly CommentDraft _draft = new();
    private long _previousResourceId;
    private int _previousSubmissionRevision;
    private string? _previousUserId;
    private bool _composing;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    protected string LoginUrl => LoginNavigation.GetLoginUrl(Navigation);
    [Parameter] public long ResourceId { get; set; }
    [Parameter] public string? CurrentUserId { get; set; }
    [Parameter] public bool CanModerate { get; set; }
    [Parameter] public bool IsAuthenticated { get; set; }
    [Parameter] public bool Saving { get; set; }
    [Parameter] public string? Message { get; set; }
    [Parameter] public int SubmissionRevision { get; set; }
    [Parameter] public CommentReadDto? PublishedComment { get; set; }
    [Parameter] public EventCallback<string> OnPost { get; set; }
    protected bool CanPost => IsAuthenticated && OnPost.HasDelegate;
    protected override void OnParametersSet()
    {
        if (_previousResourceId != ResourceId || _previousSubmissionRevision != SubmissionRevision || _previousUserId != CurrentUserId)
        {
            _draft.Text = string.Empty;
            _composing = false;
        }
        _previousResourceId = ResourceId;
        _previousSubmissionRevision = SubmissionRevision;
        _previousUserId = CurrentUserId;
    }
    protected Task PostAsync() => CanPost && !Saving && !string.IsNullOrWhiteSpace(_draft.Text)
        ? OnPost.InvokeAsync(_draft.Text.Trim()) : Task.CompletedTask;
    protected void DraftChanged(ChangeEventArgs args) => _draft.Text = args.Value?.ToString() ?? string.Empty;
    protected void BeginComment() => _composing = true;
    protected void CancelComment() { _composing = false; _draft.Text = string.Empty; }
    private sealed class CommentDraft { public string Text { get; set; } = string.Empty; }
}
