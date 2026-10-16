using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorageTesting;

public class CommentServiceTests
{
    [Fact(DisplayName = "Anonymous visitors cannot read comments from a private resource")]
    public async Task GetResourceComments_ReturnsUnauthorized_ForAnonymousPrivateAccess()
    {
        await using var database = new TestDatabase();
        await AddResource(database, visibilityId: 3);
        var service = CreateService(database);

        var result = await service.GetResourceComments(1);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.UserNotFoundUnauthorized, result.AsT1.Result);
    }

    [Fact(DisplayName = "Unrelated authenticated users cannot read comments from a private resource")]
    public async Task GetResourceComments_ReturnsForbidden_ForUnrelatedPrivateAccess()
    {
        await using var database = new TestDatabase();
        await AddResource(database, visibilityId: 3);
        var service = CreateService(database, "viewer");

        var result = await service.GetResourceComments(1);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.Forbidden, result.AsT1.Result);
    }

    [Fact(DisplayName = "The submitter can read comments from their private resource")]
    public async Task GetResourceComments_AllowsPrivateResourceSubmitter()
    {
        await using var database = new TestDatabase();
        await AddResource(database, visibilityId: 3, submitterId: "owner");
        var service = CreateService(database, "owner");

        var result = await service.GetResourceComments(1);

        Assert.True(result.IsT0);
        Assert.Empty(result.AsT0);
    }

    [Fact(DisplayName = "Moderators can read comments from private resources")]
    public async Task GetResourceComments_AllowsModeratorPrivateAccess()
    {
        await using var database = new TestDatabase();
        await AddResource(database, visibilityId: 3);
        var service = CreateService(database, "moderator", isModerator: true);

        var result = await service.GetResourceComments(1);

        Assert.True(result.IsT0);
        Assert.Empty(result.AsT0);
    }

    [Fact(DisplayName = "Comment queries distinguish a missing resource from denied access")]
    public async Task GetResourceComments_ReturnsResourceNotFound_WhenResourceDoesNotExist()
    {
        await using var database = new TestDatabase();
        var service = CreateService(database);

        var result = await service.GetResourceComments(999);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.ResourceNotFound, result.AsT1.Result);
    }

    [Fact(DisplayName = "A comment without a resource relation cannot be used by the resource-like endpoint")]
    public async Task GetUserLike_ReturnsInvalid_WhenCommentHasNoResource()
    {
        await using var database = new TestDatabase();
        database.Context.Comments.Add(new Comment
        {
            Id = 20,
            Text = "Standalone comment",
            UserId = "author",
            ScoreId = 30,
            Score = new Score { Id = 30 },
            PublishDate = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();
        var service = CreateService(database, "viewer");

        var result = await service.GetUserLike(20);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.Invalid, result.AsT1.Result);
    }

    [Fact(DisplayName = "User-like lookup resolves the vote by the comment score")]
    public async Task GetUserLike_ReturnsVote_ForCommentScore()
    {
        await using var database = new TestDatabase();
        await AddResourceComment(database);
        database.Context.LikesUsers.Add(new LikeUsers
        {
            Id = 1,
            ScoreId = 20,
            UserId = "viewer",
            Value = true
        });
        await database.Context.SaveChangesAsync();
        var service = CreateService(database, "viewer");

        var result = await service.GetUserLike(10);

        Assert.True(result.IsT0);
        Assert.Equal(1, result.AsT0.Id);
        Assert.True(result.AsT0.Value);
    }

    [Fact(DisplayName = "Changing a comment vote updates the existing score vote")]
    public async Task AddEditOrDeleteLike_UpdatesExistingVote()
    {
        await using var database = new TestDatabase();
        await AddResourceComment(database);
        database.Context.LikesUsers.Add(new LikeUsers
        {
            Id = 1,
            ScoreId = 20,
            UserId = "viewer",
            Value = true
        });
        await database.Context.SaveChangesAsync();
        var service = CreateService(database, "viewer");

        var result = await service.AddEditOrDeleteLike(10, LikeValues.Dislike);

        Assert.True(result.IsT0);
        Assert.False(result.AsT0.Value);
        Assert.Equal(1, await database.Context.LikesUsers.CountAsync());
    }

    [Fact(DisplayName = "Removing a missing comment vote returns LikeNotFound")]
    public async Task AddEditOrDeleteLike_ReturnsLikeNotFound_WhenRemovingMissingVote()
    {
        await using var database = new TestDatabase();
        await AddResourceComment(database);
        var service = CreateService(database, "viewer");

        var result = await service.AddEditOrDeleteLike(10, LikeValues.Remove);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.LikeNotFound, result.AsT1.Result);
    }

    [Fact(DisplayName = "Users cannot vote on their own comments")]
    public async Task AddEditOrDeleteLike_RejectsCommentAuthor()
    {
        await using var database = new TestDatabase();
        await AddResourceComment(database, commentAuthorId: "author");
        var service = CreateService(database, "author");

        var result = await service.AddEditOrDeleteLike(10, LikeValues.Like);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.CantLikeYourOwnComment, result.AsT1.Result);
    }

    [Theory(DisplayName = "Comment voting rejects undefined vote values")]
    [InlineData(-1)]
    [InlineData(3)]
    public async Task AddEditOrDeleteLike_RejectsUndefinedVote(int value)
    {
        await using var database = new TestDatabase();
        var service = CreateService(database, "viewer");

        var result = await service.AddEditOrDeleteLike(10, (LikeValues)value);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.UndefinedLike, result.AsT1.Result);
    }

    private static CommentService CreateService(TestDatabase database, string? userId = null,
        bool isModerator = false)
    {
        var accountService = database.CreateAccountService(userId, isModerator);
        var accessService = new ResourceAccessService(
            database.Context,
            accountService,
            database.CreateGetFirstOrError<ResourceAccessRow>(),
            NullLogger<ResourceAccessService>.Instance);

        return new CommentService(
            database.Context, database.DBHelper,
            accountService, accessService,
            database.CreateGetFirstOrDefault<LikeUsers>(),
            database.CreateGetFirstOrError<Comment>(),
            NullLogger<CommentService>.Instance);
    }

    private static async Task AddResourceComment(TestDatabase database,
        string commentAuthorId = "author")
    {
        var resource = CreateResource(visibilityId: 1);
        var commentScore = new Score { Id = 20 };
        var comment = new Comment
        {
            Id = 10,
            Text = "Resource comment",
            UserId = commentAuthorId,
            ScoreId = commentScore.Id,
            Score = commentScore,
            PublishDate = DateTime.UtcNow
        };
        comment.ResourceComment = new ResourceComments
        {
            Id = 1,
            CommentId = comment.Id,
            Comment = comment,
            ResourceId = resource.Id,
            Resource = resource
        };
        database.Context.Comments.Add(comment);
        await database.Context.SaveChangesAsync();
    }

    private static async Task AddResource(TestDatabase database, long visibilityId,
        string submitterId = "owner")
    {
        database.Context.Resources.Add(CreateResource(visibilityId, submitterId));
        await database.Context.SaveChangesAsync();
    }

    private static Resource CreateResource(long visibilityId, string submitterId = "owner")
    {
        return new Resource
        {
            Id = 1,
            Name = "Test Resource",
            Description = "A test resource description.",
            CreatedAt = DateTime.UtcNow,
            VideogameId = 1,
            ScoreId = 10,
            Score = new Score { Id = 10 },
            VisibilityId = visibilityId,
            SubmitterId = submitterId
        };
    }
}
