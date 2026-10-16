using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorageTesting;

public class FlagsServiceTests
{
    [Fact(DisplayName = "Authenticated users can report an existing resource")]
    public async Task CreateResourceFlag_StoresFlagAndResourceLink()
    {
        await using var database = new TestDatabase();
        await AddReasonAndResource(database);
        var service = CreateService(database, "reporter");

        var result = await service.Create(new ResourceFlagCreateDTO
        {
            ReasonID = 1,
            ResourceID = 1
        });

        Assert.True(result.IsT0);
        Assert.Equal("reporter", result.AsT0.UserId);
        Assert.Equal(1, result.AsT0.ReasonId);
        var link = await database.Context.ResourcesFlags.SingleAsync();
        Assert.Equal(1, link.ResourceId);
        Assert.Equal(result.AsT0.Id, link.FlagId);
    }

    [Fact(DisplayName = "Authenticated users can report an existing comment")]
    public async Task CreateCommentFlag_StoresFlagAndCommentLink()
    {
        await using var database = new TestDatabase();
        await AddReasonAndComment(database);
        var service = CreateService(database, "reporter");

        var result = await service.Create(new CommentFlagCreateDTO
        {
            ReasonID = 1,
            CommentID = 1
        });

        Assert.True(result.IsT0);
        var link = await database.Context.CommentFlags.SingleAsync();
        Assert.Equal(1, link.CommentId);
        Assert.Equal(result.AsT0.Id, link.FlagId);
    }

    [Fact(DisplayName = "Anonymous users cannot create flags")]
    public async Task CreateResourceFlag_ReturnsUnauthorized_WhenAnonymous()
    {
        await using var database = new TestDatabase();
        var service = CreateService(database);

        var result = await service.Create(new ResourceFlagCreateDTO
        {
            ReasonID = 1,
            ResourceID = 1
        });

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.Unauthorized, result.AsT1.Result);
    }

    [Fact(DisplayName = "Flag creation distinguishes a missing reason")]
    public async Task CreateResourceFlag_ReturnsReasonNotFound_WhenReasonDoesNotExist()
    {
        await using var database = new TestDatabase();
        var service = CreateService(database, "reporter");

        var result = await service.Create(new ResourceFlagCreateDTO
        {
            ReasonID = 99,
            ResourceID = 99
        });

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.ReasonNotFound, result.AsT1.Result);
    }

    [Fact(DisplayName = "A user cannot report the same resource twice")]
    public async Task CreateResourceFlag_ReturnsRepeatedFlag_ForDuplicateReport()
    {
        await using var database = new TestDatabase();
        await AddReasonAndResource(database);
        var service = CreateService(database, "reporter");
        var dto = new ResourceFlagCreateDTO { ReasonID = 1, ResourceID = 1 };
        Assert.True((await service.Create(dto)).IsT0);

        var result = await service.Create(dto);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.RepeatedFlag, result.AsT1.Result);
        Assert.Equal(1, await database.Context.Flags.CountAsync());
    }

    [Fact(DisplayName = "Regular authenticated users cannot resolve flags")]
    public async Task Edit_ReturnsForbidden_ForRegularUser()
    {
        await using var database = new TestDatabase();
        var service = CreateService(database, "reporter");

        var result = await service.Edit(1, true);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.Forbidden, result.AsT1.Result);
    }

    [Fact(DisplayName = "Moderators can resolve an existing flag")]
    public async Task Edit_UpdatesResolvedState_ForModerator()
    {
        await using var database = new TestDatabase();
        database.Context.Flags.Add(new Flag
        {
            Id = 1,
            UserId = "reporter",
            ReasonId = 1,
            CreatedAt = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();
        var service = CreateService(database, "moderator", isModerator: true);

        var result = await service.Edit(1, true);

        Assert.True(result.IsT0);
        Assert.True(result.AsT0.Resolved);
        Assert.True((await database.Context.Flags.FindAsync(1L))!.Resolved);
    }

    private static FlagsService CreateService(TestDatabase database,
        string? userId = null, bool isModerator = false)
    {
        return new FlagsService(
            database.Context,
            database.CreateAccountService(userId, isModerator),
            database.DBHelper,
            database.CreatePaginatedList<FlagReadDTO, FlagSort>(),
            database.CreateGetByProperty<Flag, long>(),
            database.CreateGetByProperty<Reason, long>(),
            database.CreateGetByProperty<Resource, long>(),
            database.CreateGetByProperty<Comment, long>(),
            NullLogger<FlagsService>.Instance);
    }

    private static async Task AddReasonAndResource(TestDatabase database)
    {
        AddReason(database);
        database.Context.Resources.Add(new Resource
        {
            Id = 1,
            Name = "Test Resource",
            Description = "A test resource description.",
            CreatedAt = DateTime.UtcNow,
            VideogameId = 1,
            ScoreId = 1,
            Score = new Score { Id = 1 },
            VisibilityId = 1,
            SubmitterId = "owner"
        });
        await database.Context.SaveChangesAsync();
    }

    private static async Task AddReasonAndComment(TestDatabase database)
    {
        AddReason(database);
        database.Context.Comments.Add(new Comment
        {
            Id = 1,
            Text = "Reported comment",
            UserId = "author",
            ScoreId = 1,
            Score = new Score { Id = 1 },
            PublishDate = DateTime.UtcNow
        });
        await database.Context.SaveChangesAsync();
    }

    private static void AddReason(TestDatabase database)
    {
        database.Context.Reasons.Add(new Reason
        {
            Id = 1,
            Description = "Spam",
            Priority = 2,
            ReasonCategoryId = 1,
            ReasonCategory = new ReasonCategory { Id = 1, Name = "Content" }
        });
    }
}
