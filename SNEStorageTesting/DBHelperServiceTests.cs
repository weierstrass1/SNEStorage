using Microsoft.EntityFrameworkCore;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorageTesting;

public class DBHelperServiceTests
{
    [Fact(DisplayName = "TryFunction returns the value produced by a successful async operation")]
    public async Task TryFunction_ReturnsValue_WhenOperationSucceeds()
    {
        await using var database = new TestDatabase();

        var result = await database.DBHelper.TryFunction(_ => Task.FromResult(42));

        Assert.True(result.IsT0);
        Assert.Equal(42, result.AsT0);
    }

    [Fact(DisplayName = "TryFunction maps an exception to the requested failure and invokes cleanup")]
    public async Task TryFunction_MapsExceptionAndInvokesCallback_WhenOperationFails()
    {
        await using var database = new TestDatabase();
        int callbackCalls = 0;

        var result = await database.DBHelper.TryFunction(
            _ => Task.FromException<int>(new InvalidOperationException("database unavailable")),
            ServiceResult.CacheError,
            () =>
            {
                callbackCalls++;
                return Task.CompletedTask;
            });

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.CacheError, result.AsT1.Result);
        Assert.Equal("database unavailable", result.AsT1.Message);
        Assert.Equal(1, callbackCalls);
    }

    [Fact(DisplayName = "A cleanup failure does not replace the original operation failure")]
    public async Task TryFunction_PreservesOriginalFailure_WhenCallbackAlsoFails()
    {
        await using var database = new TestDatabase();

        var result = await database.DBHelper.TryFunction(
            _ => Task.FromException<int>(new InvalidOperationException("original failure")),
            ServiceResult.DBError,
            () => Task.FromException(new InvalidOperationException("cleanup failure")));

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.DBError, result.AsT1.Result);
        Assert.Equal("original failure", result.AsT1.Message);
    }

    [Fact(DisplayName = "Caller cancellation is reported as a client-closed request")]
    public async Task TryFunction_ReturnsRequestCancelled_WhenCallerCancels()
    {
        await using var database = new TestDatabase();
        using var source = new CancellationTokenSource();
        source.Cancel();

        var result = await database.DBHelper.TryFunction(
            cancel => Task.FromCanceled<int>(cancel), ct: source.Token);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.RequestCancelled, result.AsT1.Result);
        Assert.Equal(ActionResultType.ClientClosedRequest, result.AsT1.Result.ActionType);
    }

    [Fact(DisplayName = "Unexpected cancellation is reported as an internal token error")]
    public async Task TryFunction_ReturnsCancellationTokenError_WhenDifferentTokenCancels()
    {
        await using var database = new TestDatabase();
        using var source = new CancellationTokenSource();
        source.Cancel();

        var result = await database.DBHelper.TryFunction(
            _ => Task.FromCanceled<int>(source.Token));

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.CancellationTokenError, result.AsT1.Result);
        Assert.Equal(ActionResultType.InternalServerError, result.AsT1.Result.ActionType);
    }

    [Fact(DisplayName = "TrySyncContext saves tracked changes and returns success")]
    public async Task TrySyncContext_SavesTrackedChanges_WhenSaveSucceeds()
    {
        await using var database = new TestDatabase();
        database.Context.Videogames.Add(new Videogame
        {
            Id = 90,
            Name = "Test Game",
            ReleaseDate = null
        });

        var result = await database.DBHelper.TrySyncContext(ct: CancellationToken.None);

        Assert.Same(ServiceResult.Success, result.Result);
        Assert.True(await database.Context.Videogames.AsNoTracking()
            .AnyAsync(game => game.Id == 90));
    }

    [Theory(DisplayName = "GetEditString preserves null, removes whitespace, and keeps actual values")]
    [InlineData(null, "existing", "existing")]
    [InlineData("", "existing", "")]
    [InlineData("   ", "existing", "")]
    [InlineData("replacement", "existing", "replacement")]
    public async Task GetEditString_AppliesPatchSemantics(string? newValue, string? oldValue,
        string? expected)
    {
        await using var database = new TestDatabase();

        string? result = database.DBHelper.GetEditString(newValue, oldValue);

        Assert.Equal(expected, result);
    }
}
