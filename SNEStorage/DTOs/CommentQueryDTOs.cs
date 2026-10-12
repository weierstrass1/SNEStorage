using SNEStorage.Services;
namespace SNEStorage.DTOs;

public sealed class CommentReadDto
{
    public long Id { get; init; }
    public required string UserId { get; init; }
    public long ScoreId { get; init; }
    public required string Text { get; init; }
    public bool Deleted { get; init; }
    public DateTime PublishDate { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int Likes { get; init; }
    public int Dislikes { get; init; }
    public LikeValues MyVote { get; init; }
    public bool HasReplies { get; init; }
    public UserNameDTO? UserSummary { get; init; }
}

public sealed record ReplyCreatedDto(long ParentCommentId, CommentReadDto Comment);
