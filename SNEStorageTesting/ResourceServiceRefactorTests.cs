using Microsoft.Extensions.Logging.Abstractions;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorageTesting;

public class ResourceServiceRefactorTests
{
    [Fact(DisplayName = "Anonymous resource suggestions exclude private resources")]
    public async Task GetResourceLike_ExcludesPrivateResources_ForAnonymousUser()
    {
        await using var database = new TestDatabase();
        await AddResources(database);
        var service = CreateService(database);

        var result = await service.GetResourceLike("Resource");

        Assert.True(result.IsT0);
        Assert.Equal([1], result.AsT0.Select(resource => resource.Id));
    }

    [Fact(DisplayName = "Resource contributors can find their own private resources")]
    public async Task GetResourceLike_IncludesPrivateResource_ForSubmitter()
    {
        await using var database = new TestDatabase();
        await AddResources(database);
        var service = CreateService(database, "private-owner");

        var result = await service.GetResourceLike("Resource");

        Assert.True(result.IsT0);
        Assert.Equal([2, 1], result.AsT0.Select(resource => resource.Id));
    }

    [Fact(DisplayName = "Staff resource suggestions include private resources")]
    public async Task GetResourceLike_IncludesPrivateResources_ForModerator()
    {
        await using var database = new TestDatabase();
        await AddResources(database);
        var service = CreateService(database, "moderator", isModerator: true);

        var result = await service.GetResourceLike("Resource");

        Assert.True(result.IsT0);
        Assert.Equal([2, 1], result.AsT0.Select(resource => resource.Id));
    }

    [Fact(DisplayName = "Resource finder rejects undefined sorting values")]
    public async Task Find_ReturnsInvalidSort_ForUndefinedSort()
    {
        await using var database = new TestDatabase();
        var service = CreateService(database);
        ResourceFinderDTO finder = new()
        {
            Sorting = new ResourceSortingDTO
            {
                SortType = (ResourceSorting)999
            }
        };

        var result = await service.Find(finder);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.InvalidSort, result.AsT1.Result);
    }

    [Fact(DisplayName = "Resource finder rejects inverted score ranges")]
    public async Task Find_ReturnsInvalidScoreRange_WhenLowerLimitExceedsUpperLimit()
    {
        await using var database = new TestDatabase();
        var service = CreateService(database);
        ResourceFinderDTO finder = new()
        {
            ScoreLowerLimit = 5,
            ScoreHigherLimit = 2
        };

        var result = await service.Find(finder);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.InvalidScoreRange, result.AsT1.Result);
    }

    [Fact(DisplayName = "Resource finder rejects date ranges that cannot form an exclusive upper bound")]
    public async Task Find_ReturnsInvalidDateRange_ForMaximumEndDate()
    {
        await using var database = new TestDatabase();
        var service = CreateService(database);
        ResourceFinderDTO finder = new() { CreatedAtTo = DateTime.MaxValue };

        var result = await service.Find(finder);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.InvalidDateRange, result.AsT1.Result);
    }

    [Theory(DisplayName = "Resource finder validates pagination before querying")]
    [InlineData(0, 0, ServiceResultTypes.InvalidMaxAmount)]
    [InlineData(ResourceService.MAX_AMOUNT_PER_PAGE + 1, 0,
        ServiceResultTypes.MaxAmountSurpassTheLimit)]
    [InlineData(10, -1, ServiceResultTypes.NegativePage)]
    [InlineData(ResourceService.MAX_AMOUNT_PER_PAGE, int.MaxValue,
        ServiceResultTypes.SkipOutOfRange)]
    public async Task Find_RejectsInvalidPagination(int maxAmount, int page,
        ServiceResultTypes expected)
    {
        await using var database = new TestDatabase();
        var service = CreateService(database);

        var result = await service.Find(new ResourceFinderDTO(), maxAmount, page);

        Assert.True(result.IsT1);
        Assert.Equal(expected, result.AsT1.Result.Type);
    }

    private static ResourceService CreateService(TestDatabase database,
        string? userId = null, bool isModerator = false)
    {
        return new ResourceService(
            database.Context,
            null!,
            null!,
            database.CreateAccountService(userId, isModerator),
            database.DBHelper,
            null!,
            null!,
            null!,
            database.CreateGetLikeBySelector<Resource, Resource>(),
            database.CreateGetFirstOrError<Resource>(),
            database.CreateGetFirstOrError<ResourceReadDTO>(),
            database.CreateGetList<ResourcePreview>(),
            NullLogger<ResourceService>.Instance);
    }

    private static async Task AddResources(TestDatabase database)
    {
        database.Context.Resources.AddRange(
            CreateResource(1, "Public Resource", 1, "public-owner"),
            CreateResource(2, "Private Resource", 3, "private-owner"));
        await database.Context.SaveChangesAsync();
    }

    private static Resource CreateResource(long id, string name,
        long visibilityId, string submitterId)
    {
        return new Resource
        {
            Id = id,
            Name = name,
            Description = "A test resource description.",
            CreatedAt = DateTime.UtcNow,
            VideogameId = 1,
            ScoreId = id,
            Score = new Score { Id = id },
            VisibilityId = visibilityId,
            SubmitterId = submitterId
        };
    }
}
