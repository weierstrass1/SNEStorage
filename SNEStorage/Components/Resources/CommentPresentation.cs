using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorage.Components.Resources;

// Presentation only: adapt creation responses and replace immutable DTOs after confirmed UI actions.
internal static class CommentPresentation
{
    public static CommentReadDto FromCreatedComment(Comment comment) => new()
    {
        Id = comment.Id,
        UserId = comment.UserId,
        ScoreId = comment.ScoreId,
        Text = comment.Text,
        Deleted = comment.Deleted,
        PublishDate = comment.PublishDate,
        UpdatedAt = comment.UpdatedAt,
        MyVote = LikeValues.Remove,
        HasReplies = comment.Childrens.Count > 0,
        UserSummary = comment.UserSummary
    };

    public static CommentReadDto Update(CommentReadDto comment, string? text = null,
        bool? deleted = null, bool? hasReplies = null, int? likes = null,
        int? dislikes = null, LikeValues? myVote = null) => new()
    {
        Id = comment.Id,
        UserId = comment.UserId,
        ScoreId = comment.ScoreId,
        Text = text ?? comment.Text,
        Deleted = deleted ?? comment.Deleted,
        PublishDate = comment.PublishDate,
        UpdatedAt = comment.UpdatedAt,
        Likes = likes ?? comment.Likes,
        Dislikes = dislikes ?? comment.Dislikes,
        MyVote = myVote ?? comment.MyVote,
        HasReplies = hasReplies ?? comment.HasReplies,
        UserSummary = comment.UserSummary
    };

    public static CommentReadDto WithVote(CommentReadDto comment, LikeValues vote) => Update(comment,
        likes: Math.Max(0, comment.Likes - (comment.MyVote == LikeValues.Like ? 1 : 0)
            + (vote == LikeValues.Like ? 1 : 0)),
        dislikes: Math.Max(0, comment.Dislikes - (comment.MyVote == LikeValues.Dislike ? 1 : 0)
            + (vote == LikeValues.Dislike ? 1 : 0)),
        myVote: vote);
}
