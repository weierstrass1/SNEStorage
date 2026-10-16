using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorageTesting;

public class NewsLikeServiceTests
{
    [Fact(DisplayName = "News reads expose the score identifier used for voting")]
    public async Task Get_ReturnsNewsScoreId()
    {
        await using var database = new TestDatabase();
        await AddNews(database);
        var service = CreateService(database);

        var result = await service.Get(1);

        Assert.True(result.IsT0);
        Assert.Equal(10, result.AsT0.ScoreId);
    }

    [Fact(DisplayName = "A first news vote creates a like for its score")]
    public async Task AddEditOrDeleteLike_CreatesLike()
    {
        await using var database = new TestDatabase();
        await AddNews(database);
        var service = CreateService(database, "voter");

        var result = await service.AddEditOrDeleteLike(1, LikeValues.Like);

        Assert.True(result.IsT0);
        Assert.True(result.AsT0.Value);
        var stored = await database.Context.LikesUsers.SingleAsync();
        Assert.Equal("voter", stored.UserId);
        Assert.Equal(10, stored.ScoreId);
    }

    [Fact(DisplayName = "A repeated news vote updates the existing row")]
    public async Task AddEditOrDeleteLike_ChangesExistingVoteWithoutDuplicate()
    {
        await using var database = new TestDatabase();
        await AddNews(database);
        database.Context.LikesUsers.Add(new LikeUsers
        {
            Id = 1,
            UserId = "voter",
            ScoreId = 10,
            Value = true
        });
        await database.Context.SaveChangesAsync();
        var service = CreateService(database, "voter");

        var result = await service.AddEditOrDeleteLike(1, LikeValues.Dislike);

        Assert.True(result.IsT0);
        Assert.False(result.AsT0.Value);
        Assert.Equal(1, await database.Context.LikesUsers.CountAsync());
    }

    [Fact(DisplayName = "Removing a news vote deletes the stored row")]
    public async Task AddEditOrDeleteLike_RemovesExistingVote()
    {
        await using var database = new TestDatabase();
        await AddNews(database);
        database.Context.LikesUsers.Add(new LikeUsers
        {
            Id = 1,
            UserId = "voter",
            ScoreId = 10,
            Value = true
        });
        await database.Context.SaveChangesAsync();
        var service = CreateService(database, "voter");

        var result = await service.AddEditOrDeleteLike(1, LikeValues.Remove);

        Assert.True(result.IsT0);
        Assert.Empty(database.Context.LikesUsers);
    }

    [Fact(DisplayName = "Removing a missing news vote returns LikeNotFound")]
    public async Task AddEditOrDeleteLike_ReturnsLikeNotFound_WhenVoteDoesNotExist()
    {
        await using var database = new TestDatabase();
        await AddNews(database);
        var service = CreateService(database, "voter");

        var result = await service.AddEditOrDeleteLike(1, LikeValues.Remove);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.LikeNotFound, result.AsT1.Result);
    }

    [Fact(DisplayName = "Anonymous users cannot vote on news")]
    public async Task AddEditOrDeleteLike_ReturnsUnauthorized_WhenAnonymous()
    {
        await using var database = new TestDatabase();
        await AddNews(database);
        var service = CreateService(database);

        var result = await service.AddEditOrDeleteLike(1, LikeValues.Like);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.Unauthorized, result.AsT1.Result);
    }

    [Theory(DisplayName = "News voting rejects undefined values")]
    [InlineData(-1)]
    [InlineData(3)]
    public async Task AddEditOrDeleteLike_ReturnsUndefinedLike_ForUnknownValue(int value)
    {
        await using var database = new TestDatabase();
        var service = CreateService(database, "voter");

        var result = await service.AddEditOrDeleteLike(1, (LikeValues)value);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.UndefinedLike, result.AsT1.Result);
    }

    [Fact(DisplayName = "Voting on a missing news item returns NewNotFound")]
    public async Task AddEditOrDeleteLike_ReturnsNewNotFound_WhenNewsDoesNotExist()
    {
        await using var database = new TestDatabase();
        var service = CreateService(database, "voter");

        var result = await service.AddEditOrDeleteLike(999, LikeValues.Like);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.NewNotFound, result.AsT1.Result);
    }

    private static NewsService CreateService(TestDatabase database, string? userId = null)
    {
        return new NewsService(
            database.Context,
            database.DBHelper,
            database.CreateAccountService(userId),
            null!,
            database.CreatePaginatedList<NewsReadDTO, NewsSorting>(),
            database.CreateGetSingleOrError<NewsReadDTO>(),
            database.CreateGetByProperty<New, long>(),
            database.CreateGetFirstOrDefault<LikeUsers>(),
            NullLogger<NewsService>.Instance);
    }

    private static async Task AddNews(TestDatabase database)
    {
        SNEStorage.Models.File avatar = new()
        {
            Id = 1,
            URL = "avatars/poster.png",
            FileTypeId = 1
        };
        ApplicationUser poster = new()
        {
            Id = "poster",
            UserName = "Poster"
        };
        poster.UserInfo = new UserInfo
        {
            Id = 1,
            UserId = poster.Id,
            User = poster,
            AvatarId = avatar.Id,
            Avatar = avatar,
            UsernameColor = "#112233"
        };
        database.Context.News.Add(new New
        {
            Id = 1,
            PosterUserId = poster.Id,
            PosterUser = poster,
            CreatedAt = DateTime.UtcNow,
            Title = "Test news",
            Text = "Test news content",
            ScoreId = 10,
            Score = new Score { Id = 10 }
        });
        await database.Context.SaveChangesAsync();
    }
}
