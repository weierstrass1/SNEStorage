using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SNEStorage.Controllers;
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
            ResourceID = 10
        });

        Assert.True(result.IsT0);
        Assert.Equal("reporter", result.AsT0.UserId);
        Assert.Equal(1, result.AsT0.ReasonId);
        var link = await database.Context.ResourcesFlags.SingleAsync();
        Assert.Equal(10, link.ResourceId);
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
        var dto = new ResourceFlagCreateDTO { ReasonID = 1, ResourceID = 10 };
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
        Assert.NotNull(result.AsT0.ResolvedAt);
        Assert.True((await database.Context.Flags.FindAsync(1L))!.Resolved);
    }

    [Fact(DisplayName = "Resolving a flag twice preserves its original resolution timestamp")]
    public async Task Edit_IsIdempotent_WhenResolutionStateDoesNotChange()
    {
        await using var database = new TestDatabase();
        DateTime resolvedAt = DateTime.UtcNow.AddDays(-1);
        database.Context.Flags.Add(new Flag
        {
            Id = 1,
            UserId = "reporter",
            ReasonId = 1,
            CreatedAt = resolvedAt.AddDays(-1),
            Resolved = true,
            ResolvedAt = resolvedAt
        });
        await database.Context.SaveChangesAsync();
        var service = CreateService(database, "moderator", isModerator: true);

        var result = await service.Edit(1, true);

        Assert.True(result.IsT0);
        Assert.Equal(resolvedAt, result.AsT0.ResolvedAt);
    }

    [Fact(DisplayName = "Reopening a flag clears its resolution timestamp")]
    public async Task Edit_ClearsResolvedAt_WhenFlagIsReopened()
    {
        await using var database = new TestDatabase();
        database.Context.Flags.Add(new Flag
        {
            Id = 1,
            UserId = "reporter",
            ReasonId = 1,
            CreatedAt = DateTime.UtcNow.AddDays(-2),
            Resolved = true,
            ResolvedAt = DateTime.UtcNow.AddDays(-1)
        });
        await database.Context.SaveChangesAsync();
        var service = CreateService(database, "moderator", isModerator: true);

        var result = await service.Edit(1, false);

        Assert.True(result.IsT0);
        Assert.False(result.AsT0.Resolved);
        Assert.Null(result.AsT0.ResolvedAt);
    }

    [Fact(DisplayName = "Editing an unknown flag returns FlagNotFound")]
    public async Task Edit_ReturnsFlagNotFound_WhenFlagDoesNotExist()
    {
        await using var database = new TestDatabase();
        var service = CreateService(database, "moderator", isModerator: true);

        var result = await service.Edit(999, true);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.FlagNotFound, result.AsT1.Result);
    }

    [Fact(DisplayName = "Already moderated resources cannot receive new reports")]
    public async Task CreateResourceFlag_ReturnsForbidden_WhenResourceIsAlreadyModerated()
    {
        await using var database = new TestDatabase();
        await AddReasonAndResource(database, alreadyModerated: true);
        var service = CreateService(database, "reporter");

        var result = await service.Create(new ResourceFlagCreateDTO
        {
            ReasonID = 1,
            ResourceID = 10
        });

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.Forbidden, result.AsT1.Result);
    }

    [Theory(DisplayName = "Only staff can list moderation flags")]
    [InlineData(null, ServiceResultTypes.Unauthorized)]
    [InlineData("regular-user", ServiceResultTypes.Unauthorized)]
    public async Task FullGet_RejectsNonStaffUsers(string? userId, ServiceResultTypes expected)
    {
        await using var database = new TestDatabase();
        var service = CreateService(database, userId);

        var result = await service.FullGet(new FlagFilterDTO(), FlagSort.HighestPriority);

        Assert.True(result.IsT1);
        Assert.Equal(expected, result.AsT1.Result.Type);
        Assert.Equal(userId is null ? ActionResultType.Unauthorized : ActionResultType.Forbidden,
            result.AsT1.Result.ActionType);
    }

    [Fact(DisplayName = "Moderation listing can filter resource reports and exposes their target")]
    public async Task FullGet_FiltersResourceFlags_AndReturnsTargetSummary()
    {
        await using var database = new TestDatabase();
        await AddReasonAndResource(database);
        var createService = CreateService(database, "reporter");
        Assert.True((await createService.Create(new ResourceFlagCreateDTO
        {
            ReasonID = 1,
            ResourceID = 10
        })).IsT0);
        await AddReporterProfile(database);
        var service = CreateService(database, "moderator", isModerator: true);

        var result = await service.FullGet(
            new FlagFilterDTO { Type = FlagType.Resource },
            FlagSort.HighestPriority);

        Assert.True(result.IsT0);
        var flag = Assert.Single(result.AsT0);
        Assert.Equal("reporter", flag.UserSummary.Id);
        Assert.NotNull(flag.Resource);
        Assert.Equal(10, flag.Resource.Id);
        Assert.Equal("Test Resource", flag.Resource.Name);
    }

    [Fact(DisplayName = "FlagController returns HTTP 401 when an anonymous user lists flags")]
    public async Task FlagController_FullGet_ReturnsUnauthorized_WhenAnonymous()
    {
        await using var database = new TestDatabase();
        var controller = new FlagController(CreateService(database), null!);

        var action = await controller.FullGet(
            new FlagFilterDTO(), FlagSort.HighestPriority);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(action.Result);
        Assert.Equal("FlagController-Unauthorized", unauthorized.Value);
    }

    [Fact(DisplayName = "FlagController returns HTTP 403 when a regular user lists flags")]
    public async Task FlagController_FullGet_ReturnsForbidden_ForRegularUser()
    {
        await using var database = new TestDatabase();
        var controller = new FlagController(
            CreateService(database, "regular-user"), null!);

        var action = await controller.FullGet(
            new FlagFilterDTO(), FlagSort.HighestPriority);

        var forbidden = Assert.IsType<ObjectResult>(action.Result);
        Assert.Equal(403, forbidden.StatusCode);
        Assert.Equal("FlagController-Unauthorized", forbidden.Value);
    }

    [Fact(DisplayName = "FlagController returns HTTP 404 when staff edits an unknown flag")]
    public async Task FlagController_Edit_ReturnsNotFound_ForUnknownFlag()
    {
        await using var database = new TestDatabase();
        var controller = new FlagController(
            CreateService(database, "moderator", isModerator: true), null!);

        var action = await controller.Edit(999, true);

        var notFound = Assert.IsType<NotFoundObjectResult>(action.Result);
        Assert.Equal("FlagController-FlagNotFound", notFound.Value);
    }

    [Fact(DisplayName = "Flag listing endpoint requires JWT authentication")]
    public void FlagController_FullGet_RequiresJwtAuthentication()
    {
        MethodInfo method = typeof(FlagController).GetMethod(
            nameof(FlagController.FullGet))!;

        var attribute = Assert.Single(
            method.GetCustomAttributes<AuthorizeAttribute>());
        Assert.Equal(JwtBearerDefaults.AuthenticationScheme,
            attribute.AuthenticationSchemes);
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

    private static async Task AddReasonAndResource(TestDatabase database,
        bool alreadyModerated = false)
    {
        AddReason(database);
        database.Context.Resources.Add(new Resource
        {
            Id = 10,
            Name = "Test Resource",
            Description = "A test resource description.",
            CreatedAt = DateTime.UtcNow,
            VideogameId = 1,
            ScoreId = 10,
            Score = new Score { Id = 10 },
            VisibilityId = 1,
            SubmitterId = "owner",
            AlreadyModerated = alreadyModerated
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

    private static async Task AddReporterProfile(TestDatabase database)
    {
        SNEStorage.Models.File avatar = new()
        {
            Id = 20,
            URL = "avatars/reporter.png",
            FileTypeId = 1
        };
        ApplicationUser reporter = new()
        {
            Id = "reporter",
            UserName = "Reporter",
            EmailConfirmed = true
        };
        reporter.UserInfo = new UserInfo
        {
            Id = 20,
            UserId = reporter.Id,
            User = reporter,
            AvatarId = avatar.Id,
            Avatar = avatar,
            UsernameColor = "#112233"
        };
        database.Context.Users.Add(reporter);
        await database.Context.SaveChangesAsync();
    }
}
