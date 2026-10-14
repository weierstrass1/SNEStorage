using Microsoft.Extensions.Logging.Abstractions;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;
using StorageTimeZone = SNEStorage.Models.TimeZone;

namespace SNEStorageTesting;

public class DateTimeServiceTests
{
    [Fact(DisplayName = "GetYear returns the current year in the user's configured time zone")]
    public async Task GetYear_ReturnsYearInConfiguredTimeZone()
    {
        await using var database = new TestDatabase();
        var timeZone = new StorageTimeZone { Id = 1, Name = "UTC-5", Value = -5 };
        database.Context.UserInfos.Add(new UserInfo
        {
            Id = 1,
            UserId = "user-1",
            TimeZoneId = timeZone.Id,
            TimeZone = timeZone
        });
        await database.Context.SaveChangesAsync();
        var service = new DateTimeService(database.Context, database.DBHelper,
            NullLogger<DateTimeService>.Instance);
        int yearBeforeCall = DateTime.UtcNow.AddHours(timeZone.Value).Year;

        var result = await service.GetYear("user-1");

        int yearAfterCall = DateTime.UtcNow.AddHours(timeZone.Value).Year;
        Assert.True(result.IsT0);
        Assert.Contains(result.AsT0, new[] { yearBeforeCall, yearAfterCall });
    }

    [Fact(DisplayName = "GetYear returns UserNotFound when the requested user does not exist")]
    public async Task GetYear_ReturnsUserNotFound_WhenUserDoesNotExist()
    {
        await using var database = new TestDatabase();
        var service = new DateTimeService(database.Context, database.DBHelper,
            NullLogger<DateTimeService>.Instance);

        var result = await service.GetYear("missing-user");

        Assert.True(result.IsT1);
        Assert.Equal(ServiceResultTypes.UserNotFound, result.AsT1.Result.Type);
    }

    [Fact(DisplayName = "GetYear returns UserNotFound when the user has no resolvable time zone")]
    public async Task GetYear_ReturnsUserNotFound_WhenTimeZoneIsMissing()
    {
        await using var database = new TestDatabase();
        database.Context.UserInfos.Add(new UserInfo
        {
            Id = 1,
            UserId = "user-1",
            TimeZoneId = 999
        });
        await database.Context.SaveChangesAsync();
        var service = new DateTimeService(database.Context, database.DBHelper,
            NullLogger<DateTimeService>.Instance);

        var result = await service.GetYear("user-1");

        Assert.True(result.IsT1);
        Assert.Equal(ServiceResultTypes.UserNotFound, result.AsT1.Result.Type);
    }
}
