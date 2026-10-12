namespace SNEStorage.Components.Resources;

public enum CommentActionKind { Reply, Edit, Delete }

public sealed record CommentActionRequest(long CommentId, CommentActionKind Kind, string? Text = null);

public sealed record CommentVoteRequest(long CommentId, bool Positive);

internal sealed class CommentActionState
{
    public string? Message { get; set; }
    public int Revision { get; set; }
    public int ReplyRevision { get; set; }
    public SNEStorage.DTOs.CommentReadDto? PublishedReply { get; set; }
}
