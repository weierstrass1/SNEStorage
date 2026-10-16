using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SNEStorage.Controllers;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorageTesting;

public class ResourceVersionServiceTests
{
    [Fact(DisplayName = "Adding a version rejects invalid version syntax before storing files")]
    public async Task AddVersion_ReturnsInvalidVersion_ForUnsupportedSyntax()
    {
        await using var database = new TestDatabase();
        var service = CreateService(database, "owner");
        ResourceVersionCreateDTO dto = new()
        {
            Version = "release-one",
            ReleaseNotes = "Release notes long enough.",
            File = new FormFile(new MemoryStream([1]), 0, 1, "file", "mod.zip")
        };

        var result = await service.AddVersion(1, dto);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.InvalidVersion, result.AsT1.Result);
    }

    [Fact(DisplayName = "Editing a version rejects values that break chronological version order")]
    public async Task EditVersion_ReturnsInvalidVersion_WhenOrderWouldBeBroken()
    {
        await using var database = new TestDatabase();
        await AddResourceWithVersions(database);
        var service = CreateService(database, "owner");

        var result = await service.EditVersion(1, 2,
            new ResourceVersionEditDTO { Version = "2.1" });

        Assert.Same(ServiceResult.InvalidVersion, result.Result);
        Assert.Equal("1.5", (await database.Context.ResourceVersions.FindAsync(2L))!.Version);
    }

    [Fact(DisplayName = "Editing a version normalizes fields and resets moderation state")]
    public async Task EditVersion_UpdatesEditableFields_AndResetsModeration()
    {
        await using var database = new TestDatabase();
        await AddResourceWithVersions(database);
        var service = CreateService(database, "owner");

        var result = await service.EditVersion(1, 2, new ResourceVersionEditDTO
        {
            Version = " 1.6-RC.1 ",
            ReleaseNotes = "  Updated release notes.  ",
            SealOfQualityRequested = true
        });

        Assert.Same(ServiceResult.Success, result.Result);
        var version = await database.Context.ResourceVersions.FindAsync(2L);
        Assert.NotNull(version);
        Assert.Equal("1.6-rc.1", version.Version);
        Assert.Equal("Updated release notes.", version.ChangeLog);
        Assert.False(version.AlreadyModerated);
        Assert.True(version.SealOfQualityRequested);
        Assert.NotNull(version.UpdatedAt);
    }

    [Fact(DisplayName = "Editing an unknown version returns VersionNotFound")]
    public async Task EditVersion_ReturnsVersionNotFound_WhenVersionDoesNotExist()
    {
        await using var database = new TestDatabase();
        await AddResourceWithVersions(database);
        var service = CreateService(database, "owner");

        var result = await service.EditVersion(1, 999, new ResourceVersionEditDTO());

        Assert.Same(ServiceResult.VersionNotFound, result.Result);
    }

    [Fact(DisplayName = "Latest version selection uses its id to break equal-date ties")]
    public async Task GetResourceLastVersion_ReturnsHighestId_WhenCreationDatesMatch()
    {
        await using var database = new TestDatabase();
        await AddResourceWithVersions(database, useEqualLatestDates: true);
        var service = CreateService(database);

        var result = await service.GetResourceLastVersion(1, CancellationToken.None);

        Assert.True(result.IsT0);
        Assert.Equal(3, result.AsT0.Id);
        Assert.Equal("2.0", result.AsT0.Version);
    }

    [Fact(DisplayName = "Deleting an unknown resource version returns VersionNotFound")]
    public async Task DeleteVersion_ReturnsVersionNotFound_WhenVersionDoesNotExist()
    {
        await using var database = new TestDatabase();
        await AddResourceWithVersions(database);
        var service = CreateService(database, "owner");

        var result = await service.DeleteVersion(1, 999);

        Assert.Same(ServiceResult.VersionNotFound, result.Result);
    }

    [Fact(DisplayName = "The only version of a resource cannot be deleted")]
    public async Task DeleteVersion_ReturnsCantDeleteOnlyVersion_ForSingleVersion()
    {
        await using var database = new TestDatabase();
        await AddResourceWithVersions(database, singleVersion: true);
        var service = CreateService(database, "owner");

        var result = await service.DeleteVersion(1, 1);

        Assert.Same(ServiceResult.CantDeleteOnlyVersion, result.Result);
    }

    [Fact(DisplayName = "Contributors cannot delete quality-sealed versions")]
    public async Task DeleteVersion_ReturnsVersionHasSealOfQuality_ForContributor()
    {
        await using var database = new TestDatabase();
        await AddResourceWithVersions(database);
        (await database.Context.ResourceVersions.FindAsync(2L))!.SealOfQuality = true;
        await database.Context.SaveChangesAsync();
        var service = CreateService(database, "owner");

        var result = await service.DeleteVersion(1, 2);

        Assert.Same(ServiceResult.VersionHasSealOfQuality, result.Result);
    }

    [Fact(DisplayName = "Unsealed non-final versions pass deletion validation")]
    public async Task ValidateDelete_ReturnsSuccess_ForUnsealedNonFinalVersion()
    {
        await using var database = new TestDatabase();
        await AddResourceWithVersions(database);
        var service = CreateService(database, "owner");
        var findResult = await service.FindDelete((1, 2));
        Assert.True(findResult.IsT0);

        var result = await service.ValidateDelete(findResult.AsT0);

        Assert.Same(ServiceResult.Success, result.Result);
    }

    [Fact(DisplayName = "Unrelated users cannot delete resource versions")]
    public async Task DeleteVersion_ReturnsForbidden_ForUnrelatedUser()
    {
        await using var database = new TestDatabase();
        await AddResourceWithVersions(database);
        var service = CreateService(database, "viewer");

        var result = await service.DeleteVersion(1, 2);

        Assert.Same(ServiceResult.Forbidden, result.Result);
    }

    [Fact(DisplayName = "Delete-version endpoint maps final-version conflicts to HTTP 409")]
    public async Task ResourceController_DeleteVersion_ReturnsConflict_ForOnlyVersion()
    {
        await using var database = new TestDatabase();
        await AddResourceWithVersions(database, singleVersion: true);
        var service = CreateService(database, "owner");
        var controller = new ResourceController(null!, null!, null!, service);

        var action = await controller.DeleteVersion(1, 1);

        var conflict = Assert.IsType<ConflictObjectResult>(action);
        Assert.Equal("ResourceController-CantDeleteOnlyVersion", conflict.Value);
    }

    private static ResourceVersionService CreateService(TestDatabase database,
        string? userId = null)
    {
        var accountService = database.CreateAccountService(userId);
        var accessService = new ResourceAccessService(
            database.Context, accountService,
            database.CreateGetFirstOrError<ResourceAccessRow>(),
            NullLogger<ResourceAccessService>.Instance);
        return new ResourceVersionService(
            null!, database.Context, null!, accountService, accessService,
            database.DBHelper,
            database.CreateGetFirstOrError<Resource>(),
            database.CreateGetFirstOrError<SNEStorage.Models.File>(),
            database.CreateGetList<ResourceVersion>(),
            database.CreateGetFirstOrError<ResourceVersion>(),
            NullLogger<ResourceVersionService>.Instance);
    }

    private static async Task AddResourceWithVersions(TestDatabase database,
        bool useEqualLatestDates = false, bool singleVersion = false)
    {
        DateTime firstDate = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        DateTime secondDate = firstDate.AddDays(1);
        DateTime thirdDate = useEqualLatestDates ? secondDate : firstDate.AddDays(2);
        Score score = new() { Id = 10 };
        Resource resource = new()
        {
            Id = 1,
            Name = "Test Resource",
            Description = "A test resource description.",
            CreatedAt = firstDate,
            VideogameId = 1,
            ScoreId = score.Id,
            Score = score,
            VisibilityId = 1,
            SubmitterId = "owner"
        };
        resource.ResourceVersions = singleVersion ?
        [
            CreateVersion(1, resource, "1.0", firstDate)
        ] :
        [
            CreateVersion(1, resource, "1.0", firstDate),
            CreateVersion(2, resource, "1.5", secondDate, alreadyModerated: true),
            CreateVersion(3, resource, "2.0", thirdDate)
        ];

        database.Context.Resources.Add(resource);
        await database.Context.SaveChangesAsync();
    }

    private static ResourceVersion CreateVersion(long id, Resource resource,
        string version, DateTime createdAt, bool alreadyModerated = false)
    {
        return new ResourceVersion
        {
            Id = id,
            ResourceId = resource.Id,
            Resource = resource,
            FileId = id,
            File = new SNEStorage.Models.File
            {
                Id = id,
                URL = $"resources/version-{id}.zip",
                FileTypeId = 1
            },
            SubmitterUserId = "uploader",
            CreatedAt = createdAt,
            Version = version,
            ChangeLog = "Original release notes.",
            AlreadyModerated = alreadyModerated
        };
    }
}
