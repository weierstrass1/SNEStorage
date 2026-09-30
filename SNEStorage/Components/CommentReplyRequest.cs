namespace SNEStorage.Components;

public sealed record CommentReplyRequest(long ParentCommentId, string Text);
