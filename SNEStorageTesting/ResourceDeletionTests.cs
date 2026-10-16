using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SNEStorage.Controllers;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorageTesting;

public class ResourceDeletionTests
{
    [Fact(DisplayName = "Anonymous users cannot delete resources")]
    public async Task Delete_ReturnsUnauthorized_WhenAnonymous()
    {
        await using var database = new TestDatabase();
        var service = CreateService(database);

        var result = await service.Delete(1);

        Assert.Same(ServiceResult.Unauthorized, result.Result);
    }

    [Fact(DisplayName = "Deleting an unknown resource returns ResourceNotFound")]
    public async Task Delete_ReturnsResourceNotFound_WhenResourceDoesNotExist()
    {
        await using var database = new TestDatabase();
        var service = CreateService(database, "owner");

        var result = await service.Delete(999);

        Assert.Same(ServiceResult.ResourceNotFound, result.Result);
    }

    [Fact(DisplayName = "Only contributors or staff can delete a resource")]
    public async Task Delete_ReturnsForbidden_ForUnrelatedUser()
    {
        await using var database = new TestDatabase();
        await AddResource(database);
        var service = CreateService(database, "viewer");

        var result = await service.Delete(1);

        Assert.Same(ServiceResult.Forbidden, result.Result);
    }

    [Fact(DisplayName = "Contributors cannot delete resources with quality-sealed versions")]
    public async Task Delete_ReturnsResourceHasSealOfQuality_ForContributor()
    {
        await using var database = new TestDatabase();
        await AddResource(database, hasSeal: true);
        var service = CreateService(database, "owner");

        var result = await service.Delete(1);

        Assert.Same(ServiceResult.ResourceHasSealOfQuality, result.Result);
    }

    [Fact(DisplayName = "Contributors cannot delete resources required by another resource")]
    public async Task Delete_ReturnsResourceHasDependents_ForContributor()
    {
        await using var database = new TestDatabase();
        await AddResource(database, hasDependent: true);
        var service = CreateService(database, "owner");

        var result = await service.Delete(1);

        Assert.Same(ServiceResult.ResourceHasDependents, result.Result);
    }

    [Fact(DisplayName = "A plain contributor-owned resource passes deletion validation")]
    public async Task ValidateDelete_ReturnsSuccess_ForUnrestrictedContributorResource()
    {
        await using var database = new TestDatabase();
        await AddResource(database);
        var service = CreateService(database, "owner");
        var findResult = await service.FindDelete(1);
        Assert.True(findResult.IsT0);

        var result = await service.ValidateDelete(findResult.AsT0);

        Assert.Same(ServiceResult.Success, result.Result);
    }

    [Fact(DisplayName = "Resource details explain when a quality seal blocks deletion")]
    public async Task Get_ReturnsSealAsDeleteBlockReason()
    {
        await using var database = new TestDatabase();
        await AddResource(database, hasSeal: true);
        var service = CreateService(database, "owner");

        var result = await service.Get(1);

        Assert.True(result.IsT0);
        Assert.True(result.AsT0.CanEdit);
        Assert.False(result.AsT0.CanDelete);
        Assert.Equal(ServiceResultTypes.ResourceHasSealOfQuality,
            result.AsT0.DeleteBlockReason);
    }

    [Fact(DisplayName = "Resource details explain when dependents block deletion")]
    public async Task Get_ReturnsDependentsAsDeleteBlockReason()
    {
        await using var database = new TestDatabase();
        await AddResource(database, hasDependent: true);
        var service = CreateService(database, "owner");

        var result = await service.Get(1);

        Assert.True(result.IsT0);
        Assert.False(result.AsT0.CanDelete);
        Assert.Equal(ServiceResultTypes.ResourceHasDependents,
            result.AsT0.DeleteBlockReason);
    }

    [Fact(DisplayName = "Staff can delete restricted resources according to resource details")]
    public async Task Get_AllowsStaffDeletion_DespiteContributorRestrictions()
    {
        await using var database = new TestDatabase();
        await AddResource(database, hasSeal: true, hasDependent: true);
        var service = CreateService(database, "moderator", isModerator: true);

        var result = await service.Get(1);

        Assert.True(result.IsT0);
        Assert.True(result.AsT0.CanDelete);
        Assert.Null(result.AsT0.DeleteBlockReason);
    }

    [Fact(DisplayName = "Resource delete endpoint maps deletion conflicts to HTTP 409")]
    public async Task ResourceController_DeleteResource_ReturnsConflict_ForSealedResource()
    {
        await using var database = new TestDatabase();
        await AddResource(database, hasSeal: true);
        var service = CreateService(database, "owner");
        var controller = new ResourceController(null!, service, null!, null!);

        var action = await controller.DeleteResource(1);

        var conflict = Assert.IsType<ConflictObjectResult>(action);
        Assert.Equal("ResourceController-ResourceHasSealOfQuality", conflict.Value);
    }

    private static ResourceService CreateService(TestDatabase database,
        string? userId = null, bool isModerator = false)
    {
        var accountService = database.CreateAccountService(userId, isModerator);
        var accessService = new ResourceAccessService(
            database.Context,
            accountService,
            database.CreateGetFirstOrError<ResourceAccessRow>(),
            NullLogger<ResourceAccessService>.Instance);
        return new ResourceService(
            database.Context,
            null!,
            null!,
            accountService,
            database.DBHelper,
            null!,
            accessService,
            null!,
            database.CreateGetLikeBySelector<Resource, Resource>(),
            database.CreateGetFirstOrError<Resource>(),
            database.CreateGetFirstOrError<ResourceReadDTO>(),
            database.CreateGetList<ResourcePreview>(),
            NullLogger<ResourceService>.Instance);
    }

    private static async Task AddResource(TestDatabase database,
        bool hasSeal = false, bool hasDependent = false)
    {
        Videogame videogame = new() { Id = 1, Name = "Game", ReleaseDate = null };
        Visibility visibility = new() { Id = 1, Name = "Public", ResourceOnly = true };
        Resource resource = CreateResource(1, "Target Resource", "owner", videogame, visibility);

        if (hasSeal)
        {
            resource.ResourceVersions.Add(new ResourceVersion
            {
                Id = 1,
                ResourceId = resource.Id,
                Resource = resource,
                FileId = 1,
                SubmitterUserId = "owner",
                CreatedAt = DateTime.UtcNow,
                Version = "1.0",
                SealOfQuality = true
            });
        }

        database.Context.Resources.Add(resource);

        if (hasDependent)
        {
            Resource dependent = CreateResource(
                2, "Dependent Resource", "other-owner", videogame, visibility);
            dependent.ResourceDependencies.Add(new ResourceDependency
            {
                Id = 1,
                ResourceId = dependent.Id,
                Resource = dependent,
                DependencyResourceId = resource.Id,
                DependencyResource = resource
            });
            database.Context.Resources.Add(dependent);
        }

        await database.Context.SaveChangesAsync();
    }

    private static Resource CreateResource(long id, string name, string submitterId,
        Videogame videogame, Visibility visibility)
    {
        return new Resource
        {
            Id = id,
            Name = name,
            Description = "A test resource description.",
            CreatedAt = DateTime.UtcNow,
            VideogameId = videogame.Id,
            Videogame = videogame,
            ScoreId = id,
            Score = new Score { Id = id },
            VisibilityId = visibility.Id,
            Visibility = visibility,
            SubmitterId = submitterId
        };
    }
}
