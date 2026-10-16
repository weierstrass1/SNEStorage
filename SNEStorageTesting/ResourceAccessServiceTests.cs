using Microsoft.Extensions.Logging.Abstractions;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorageTesting;

public class ResourceAccessServiceTests
{
    [Fact(DisplayName = "Public resources are visible without an authenticated account")]
    public async Task GetViewAccess_AllowsAnonymousPublicAccess()
    {
        await using var database = new TestDatabase();
        await AddResource(database, visibilityId: 1);
        var service = CreateService(database);

        var result = await service.GetViewAccess(1);

        Assert.True(result.IsT0);
        Assert.False(result.AsT0.IsContributor);
    }

    [Fact(DisplayName = "Anonymous private-resource access returns the unauthorized UserNotFound variant")]
    public async Task GetViewAccess_ReturnsUnauthorized_WhenPrivateAndAnonymous()
    {
        await using var database = new TestDatabase();
        await AddResource(database, visibilityId: 3);
        var service = CreateService(database);

        var result = await service.GetViewAccess(1);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.UserNotFoundUnauthorized, result.AsT1.Result);
        Assert.Equal(ActionResultType.Unauthorized, result.AsT1.Result.ActionType);
    }

    [Fact(DisplayName = "Unrelated authenticated users receive forbidden for private resources")]
    public async Task GetViewAccess_ReturnsForbidden_WhenPrivateAndUnrelated()
    {
        await using var database = new TestDatabase();
        await AddResource(database, visibilityId: 3);
        var service = CreateService(database, "viewer");

        var result = await service.GetViewAccess(1);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.Forbidden, result.AsT1.Result);
    }

    [Theory(DisplayName = "Private resources are visible to their contributors")]
    [InlineData("owner", "owner", null, null)]
    [InlineData("author", "owner", "author", null)]
    [InlineData("version-uploader", "owner", null, "version-uploader")]
    public async Task GetViewAccess_AllowsEveryContributorType(string userId,
        string submitterId, string? authorId, string? versionSubmitterId)
    {
        await using var database = new TestDatabase();
        await AddResource(database, 3, submitterId, authorId, versionSubmitterId);
        var service = CreateService(database, userId);

        var result = await service.GetViewAccess(1);

        Assert.True(result.IsT0);
        Assert.True(result.AsT0.IsContributor);
    }

    [Fact(DisplayName = "Staff can view a private resource without becoming a contributor")]
    public async Task GetViewAccess_AllowsStaffWithoutContributorFlag()
    {
        await using var database = new TestDatabase();
        await AddResource(database, visibilityId: 3);
        var service = CreateService(database, "moderator", isModerator: true);

        var result = await service.GetViewAccess(1);

        Assert.True(result.IsT0);
        Assert.False(result.AsT0.IsContributor);
    }

    [Fact(DisplayName = "Only contributors receive edit access even when a resource is public")]
    public async Task GetEditAccess_ReturnsForbidden_ForAuthenticatedNonContributor()
    {
        await using var database = new TestDatabase();
        await AddResource(database, visibilityId: 1);
        var service = CreateService(database, "viewer");

        var result = await service.GetEditAccess(1);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.Forbidden, result.AsT1.Result);
    }

    [Fact(DisplayName = "Missing resources remain distinguishable from access denial")]
    public async Task GetAccess_ReturnsResourceNotFound_WhenResourceDoesNotExist()
    {
        await using var database = new TestDatabase();
        var service = CreateService(database, "viewer");

        var result = await service.GetAccess(999);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.ResourceNotFound, result.AsT1.Result);
    }

    [Fact(DisplayName = "Anonymous users cannot view age-restricted resources")]
    public async Task GetViewAccess_ReturnsUnauthorized_ForAnonymousSensitiveContent()
    {
        await using var database = new TestDatabase();
        await AddResource(database, visibilityId: 1, includesSensitiveContent: true);
        var service = CreateService(database);

        var result = await service.GetViewAccess(1);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.UserNotFoundUnauthorized, result.AsT1.Result);
    }

    [Fact(DisplayName = "Underage users cannot view age-restricted resources")]
    public async Task GetViewAccess_ReturnsForbidden_ForUnderageSensitiveContent()
    {
        await using var database = new TestDatabase();
        await AddResource(database, visibilityId: 1, includesSensitiveContent: true);
        database.Context.UserInfos.Add(new UserInfo
        {
            UserId = "viewer",
            Birthday = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-17)
        });
        await database.Context.SaveChangesAsync();
        var service = CreateService(database, "viewer");

        var result = await service.GetViewAccess(1);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.Forbidden, result.AsT1.Result);
    }

    [Fact(DisplayName = "Adult users can view age-restricted public resources")]
    public async Task GetViewAccess_AllowsAdultSensitiveContent()
    {
        await using var database = new TestDatabase();
        await AddResource(database, visibilityId: 1, includesSensitiveContent: true);
        database.Context.UserInfos.Add(new UserInfo
        {
            UserId = "viewer",
            Birthday = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-18)
        });
        await database.Context.SaveChangesAsync();
        var service = CreateService(database, "viewer");

        var result = await service.GetViewAccess(1);

        Assert.True(result.IsT0);
        Assert.True(result.AsT0.ContentAllowed);
    }

    private static ResourceAccessService CreateService(TestDatabase database,
        string? userId = null, bool isModerator = false)
    {
        return new ResourceAccessService(
            database.Context,
            database.CreateAccountService(userId, isModerator),
            database.CreateGetFirstOrError<ResourceAccessRow>(),
            NullLogger<ResourceAccessService>.Instance);
    }

    private static async Task AddResource(TestDatabase database, long visibilityId,
        string submitterId = "owner", string? authorId = null,
        string? versionSubmitterId = null, bool includesSensitiveContent = false)
    {
        Resource resource = new()
        {
            Id = 1,
            Name = "Test Resource",
            Description = "A test resource description.",
            CreatedAt = DateTime.UtcNow,
            VideogameId = 1,
            ScoreId = 10,
            Score = new Score { Id = 10 },
            VisibilityId = visibilityId,
            SubmitterId = submitterId,
            IncludesSensitiveContent = includesSensitiveContent
        };

        if (authorId is not null)
        {
            resource.ResourcesAuthors.Add(new ResourceAuthors
            {
                Id = 1,
                ResourceId = resource.Id,
                Resource = resource,
                UserId = authorId
            });
        }

        if (versionSubmitterId is not null)
        {
            resource.ResourceVersions.Add(new ResourceVersion
            {
                Id = 1,
                ResourceId = resource.Id,
                Resource = resource,
                FileId = 1,
                SubmitterUserId = versionSubmitterId,
                CreatedAt = DateTime.UtcNow
            });
        }

        database.Context.Resources.Add(resource);
        await database.Context.SaveChangesAsync();
    }
}
