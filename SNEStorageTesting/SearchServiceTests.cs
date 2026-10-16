using Microsoft.Extensions.Logging.Abstractions;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorageTesting;

public class SearchServiceTests
{
    [Theory(DisplayName = "Videogame search rejects non-positive and excessive result limits")]
    [InlineData(0, ServiceResultTypes.InvalidMaxAmount)]
    [InlineData(-1, ServiceResultTypes.InvalidMaxAmount)]
    [InlineData(VideogameService.MAX_AMOUNT_LIMIT + 1,
        ServiceResultTypes.MaxAmountSurpassTheLimit)]
    public async Task VideogameSearch_RejectsInvalidLimits(int maxAmount,
        ServiceResultTypes expected)
    {
        await using var database = new TestDatabase();
        var service = new VideogameService(database.Context,
            database.CreateGetLikeBySelector<Videogame, Videogame>());

        var result = await service.GetVideogamesLike("mario", maxAmount);

        Assert.True(result.IsT1);
        Assert.Equal(expected, result.AsT1.Result.Type);
    }

    [Theory(DisplayName = "Short videogame searches return only Any and Other in stable order")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("a")]
    public async Task VideogameSearch_ReturnsSpecialOptions_ForShortTerms(string? term)
    {
        await using var database = new TestDatabase();
        database.Context.Videogames.AddRange(
            new Videogame { Id = 3, Name = "Zelda", ReleaseDate = null },
            new Videogame { Id = 2, Name = "Other", ReleaseDate = null },
            new Videogame { Id = 1, Name = "Any", ReleaseDate = null });
        await database.Context.SaveChangesAsync();
        var service = new VideogameService(database.Context,
            database.CreateGetLikeBySelector<Videogame, Videogame>());

        var result = await service.GetVideogamesLike(term!, 10);

        Assert.True(result.IsT0);
        Assert.Equal([1, 2], result.AsT0.Select(game => game.Id));
    }

    [Fact(DisplayName = "Videogame search treats SQL wildcard characters as literal text")]
    public async Task VideogameSearch_EscapesWildcardCharacters()
    {
        await using var database = new TestDatabase();
        database.Context.Videogames.AddRange(
            new Videogame { Id = 3, Name = "a_game", ReleaseDate = null },
            new Videogame { Id = 4, Name = "abgame", ReleaseDate = null });
        await database.Context.SaveChangesAsync();
        var service = new VideogameService(database.Context,
            database.CreateGetLikeBySelector<Videogame, Videogame>());

        var result = await service.GetVideogamesLike("a_", 10);

        Assert.True(result.IsT0);
        Assert.Equal("a_game", Assert.Single(result.AsT0).Name);
    }

    [Theory(DisplayName = "External-author search rejects non-positive and excessive result limits")]
    [InlineData(0, ServiceResultTypes.InvalidMaxAmount)]
    [InlineData(ExternalAuthorService.MAX_AMOUNT_LIMIT + 1,
        ServiceResultTypes.MaxAmountSurpassTheLimit)]
    public async Task ExternalAuthorSearch_RejectsInvalidLimits(int maxAmount,
        ServiceResultTypes expected)
    {
        await using var database = new TestDatabase();
        var service = new ExternalAuthorService(database.Context,
            database.CreateGetLikeBySelector<string, string>());

        var result = await service.GetAuthorLike("author", maxAmount);

        Assert.True(result.IsT1);
        Assert.Equal(expected, result.AsT1.Result.Type);
    }

    [Fact(DisplayName = "External-author search removes duplicates, sorts, and applies its limit")]
    public async Task ExternalAuthorSearch_ReturnsDistinctSortedLimitedNames()
    {
        await using var database = new TestDatabase();
        database.Context.ResourcesExternalAuthors.AddRange(
            new ResourceExternalAuthor { Id = 1, ResourceId = 1, Author = "charlie" },
            new ResourceExternalAuthor { Id = 2, ResourceId = 2, Author = "alpha" },
            new ResourceExternalAuthor { Id = 3, ResourceId = 3, Author = "charlie" },
            new ResourceExternalAuthor { Id = 4, ResourceId = 4, Author = "bravo" });
        await database.Context.SaveChangesAsync();
        var service = new ExternalAuthorService(database.Context,
            database.CreateGetLikeBySelector<string, string>());

        var result = await service.GetAuthorLike(string.Empty, 2);

        Assert.True(result.IsT0);
        Assert.Equal(["alpha", "bravo"], result.AsT0);
    }

    [Fact(DisplayName = "External-author search trims its term and filters matching names")]
    public async Task ExternalAuthorSearch_TrimsAndFiltersTerm()
    {
        await using var database = new TestDatabase();
        database.Context.ResourcesExternalAuthors.AddRange(
            new ResourceExternalAuthor { Id = 1, ResourceId = 1, Author = "banana" },
            new ResourceExternalAuthor { Id = 2, ResourceId = 2, Author = "manual" },
            new ResourceExternalAuthor { Id = 3, ResourceId = 3, Author = "zelda" });
        await database.Context.SaveChangesAsync();
        var service = new ExternalAuthorService(database.Context,
            database.CreateGetLikeBySelector<string, string>());

        var result = await service.GetAuthorLike("  an  ", 10);

        Assert.True(result.IsT0);
        Assert.Equal(["banana", "manual"], result.AsT0);
    }

    [Theory(DisplayName = "User search rejects non-positive and excessive result limits")]
    [InlineData(0, ServiceResultTypes.InvalidMaxAmount)]
    [InlineData(UserService.MAX_AMOUNT_LIMIT + 1,
        ServiceResultTypes.MaxAmountSurpassTheLimit)]
    public async Task UserSearch_RejectsInvalidLimits(int maxAmount, ServiceResultTypes expected)
    {
        await using var database = new TestDatabase();
        var service = new UserService(database.Context,
            database.CreateGetLikeBySelector<UserInfo, UserNameDTO>());

        var result = await service.GetUserLike("user", maxAmount);

        Assert.True(result.IsT1);
        Assert.Equal(expected, result.AsT1.Result.Type);
    }

    [Fact(DisplayName = "User search returns sorted public summaries and applies its limit")]
    public async Task UserSearch_ReturnsSortedLimitedPublicSummaries()
    {
        await using var database = new TestDatabase();
        AddUser(database.Context, "user-z", "zulu", "#333333");
        AddUser(database.Context, "user-a", "alpha", "#111111");
        AddUser(database.Context, "user-b", "bravo", "#222222");
        await database.Context.SaveChangesAsync();
        var service = new UserService(database.Context,
            database.CreateGetLikeBySelector<UserInfo, UserNameDTO>());

        var result = await service.GetUserLike(string.Empty, 2);

        Assert.True(result.IsT0);
        Assert.Equal(["alpha", "bravo"], result.AsT0.Select(user => user.Username));
        Assert.Equal(["user-a", "user-b"], result.AsT0.Select(user => user.Id));
        Assert.Equal(["#111111", "#222222"],
            result.AsT0.Select(user => user.UsernameColor));
    }

    [Fact(DisplayName = "User search trims its term and filters matching usernames")]
    public async Task UserSearch_TrimsAndFiltersTerm()
    {
        await using var database = new TestDatabase();
        AddUser(database.Context, "user-1", "banana", "#111111");
        AddUser(database.Context, "user-2", "manual", "#222222");
        AddUser(database.Context, "user-3", "zelda", "#333333");
        await database.Context.SaveChangesAsync();
        var service = new UserService(database.Context,
            database.CreateGetLikeBySelector<UserInfo, UserNameDTO>());

        var result = await service.GetUserLike("  an  ", 10);

        Assert.True(result.IsT0);
        Assert.Equal(["banana", "manual"], result.AsT0.Select(user => user.Username));
    }

    private static void AddUser(SnestorageContext context, string id, string username,
        string color)
    {
        var user = new ApplicationUser { Id = id, UserName = username };
        context.UserInfos.Add(new UserInfo
        {
            UserId = id,
            User = user,
            UsernameColor = color
        });
    }
}
