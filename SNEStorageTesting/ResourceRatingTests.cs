using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SNEStorage.Controllers;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorageTesting;

public class ResourceRatingTests
{
    [Fact(DisplayName = "A first resource rating stores and returns the selected value")]
    public async Task AddOrEditScore_CreatesVote_WithSelectedValue()
    {
        await using var database = new TestDatabase();
        await AddResource(database, visibilityId: 1);
        var service = CreateService(database, "voter");

        var result = await service.AddOrEditScore(1, 4);

        Assert.True(result.IsT0);
        Assert.Equal(4, result.AsT0.Value);
        var stored = await database.Context.ScoresUsers.AsNoTracking().SingleAsync();
        Assert.Equal("voter", stored.UserId);
        Assert.Equal(10, stored.ScoreId);
        Assert.Equal(4, stored.Value);
    }

    [Fact(DisplayName = "A repeated resource rating updates the existing vote instead of adding another")]
    public async Task AddOrEditScore_UpdatesExistingVote()
    {
        await using var database = new TestDatabase();
        await AddResource(database, visibilityId: 1);
        database.Context.ScoresUsers.Add(new ScoreUsers
        {
            Id = 1,
            ScoreId = 10,
            UserId = "voter",
            Value = 2
        });
        await database.Context.SaveChangesAsync();
        var service = CreateService(database, "voter");

        var result = await service.AddOrEditScore(1, 5);

        Assert.True(result.IsT0);
        Assert.Equal(5, result.AsT0.Value);
        Assert.Equal(1, await database.Context.ScoresUsers.CountAsync());
        Assert.Equal(5, (await database.Context.ScoresUsers.SingleAsync()).Value);
    }

    [Theory(DisplayName = "Resource ratings reject values outside the one-to-five range")]
    [InlineData(0)]
    [InlineData(6)]
    public async Task AddOrEditScore_RejectsOutOfRangeValue(long value)
    {
        await using var database = new TestDatabase();
        var service = CreateService(database, "voter");

        var result = await service.AddOrEditScore(1, value);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.InvalidScore, result.AsT1.Result);
    }

    [Fact(DisplayName = "Anonymous users cannot rate resources")]
    public async Task AddOrEditScore_ReturnsForbidden_WhenUserIsAnonymous()
    {
        await using var database = new TestDatabase();
        var service = CreateService(database, null);

        var result = await service.AddOrEditScore(1, 3);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.Forbidden, result.AsT1.Result);
    }

    [Fact(DisplayName = "Private resources cannot receive ratings")]
    public async Task AddOrEditScore_RejectsPrivateResource()
    {
        await using var database = new TestDatabase();
        await AddResource(database, visibilityId: 3);
        var service = CreateService(database, "voter");

        var result = await service.AddOrEditScore(1, 3);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.CantAddScoreToPrivateResource, result.AsT1.Result);
    }

    [Fact(DisplayName = "Resource contributors cannot rate their own resource")]
    public async Task AddOrEditScore_RejectsResourceContributor()
    {
        await using var database = new TestDatabase();
        await AddResource(database, visibilityId: 1, submitterId: "owner");
        var service = CreateService(database, "owner");

        var result = await service.AddOrEditScore(1, 3);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.CantScoreSelfResource, result.AsT1.Result);
    }

    [Fact(DisplayName = "ResourceController maps an invalid rating to its public error response")]
    public async Task ResourceController_ReturnsBadRequest_ForInvalidRating()
    {
        await using var database = new TestDatabase();
        var service = CreateService(database, "voter");
        var controller = new ResourceController(null!, null!, service, null!);

        var action = await controller.AddOrEditScore(1, 0, CancellationToken.None);

        var result = Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal("ResourceController-InvalidScore", result.Value);
    }

    private static ResourceCommunityService CreateService(TestDatabase database, string? userId)
    {
        var accountService = database.CreateAccountService(userId);
        var accessService = new ResourceAccessService(
            database.Context, accountService, database.DBHelper,
            NullLogger<ResourceAccessService>.Instance);
        return new ResourceCommunityService(
            database.Context, accountService, accessService, database.DBHelper,
            NullLogger<ResourceCommunityService>.Instance);
    }

    private static async Task AddResource(TestDatabase database, long visibilityId,
        string submitterId = "owner")
    {
        var score = new Score { Id = 10 };
        database.Context.Resources.Add(new Resource
        {
            Id = 1,
            Name = "Test Resource",
            Description = "A test resource description.",
            CreatedAt = DateTime.UtcNow,
            VideogameId = 1,
            ScoreId = score.Id,
            Score = score,
            VisibilityId = visibilityId,
            SubmitterId = submitterId
        });
        await database.Context.SaveChangesAsync();
    }
}
