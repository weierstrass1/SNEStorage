using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SNEStorage.Controllers;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorageTesting;

public class SearchControllerTests
{
    [Fact(DisplayName = "VideogameController maps invalid limits to its documented bad request")]
    public async Task VideogameController_ReturnsBadRequest_WhenLimitIsInvalid()
    {
        await using var database = new TestDatabase();
        var service = new VideogameService(database.Context,
            database.CreateGetLikeBySelector<Videogame, Videogame>());
        var controller = new VideogameController(service);

        var action = await controller.GetVideogamesLike("mario", 0);

        var result = Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal("VideogameController-InvalidMaxAmount", result.Value);
    }

    [Fact(DisplayName = "VideogameController returns the special options from its service")]
    public async Task VideogameController_ReturnsOk_WithSpecialOptions()
    {
        await using var database = new TestDatabase();
        database.Context.Videogames.AddRange(
            new Videogame { Id = 2, Name = "Other", ReleaseDate = null },
            new Videogame { Id = 1, Name = "Any", ReleaseDate = null });
        await database.Context.SaveChangesAsync();
        var service = new VideogameService(database.Context,
            database.CreateGetLikeBySelector<Videogame, Videogame>());
        var controller = new VideogameController(service);

        var action = await controller.GetVideogamesLike(null, 10);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        var games = Assert.IsType<List<Videogame>>(result.Value);
        Assert.Equal([1, 2], games.Select(game => game.Id));
    }

    [Fact(DisplayName = "ExternalAuthorController prefixes validation errors with its controller name")]
    public async Task ExternalAuthorController_ReturnsBadRequest_WhenLimitExceedsMaximum()
    {
        await using var database = new TestDatabase();
        var service = new ExternalAuthorService(database.Context,
            database.CreateGetLikeBySelector<string, string>());
        var controller = new ExternalAuthorController(service);

        var action = await controller.GetExternalAuthorsLike(
            "author", ExternalAuthorService.MAX_AMOUNT_LIMIT + 1);

        var result = Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal("ExternalAuthorController-MaxAmountSurpassTheLimit", result.Value);
    }

    [Fact(DisplayName = "ExternalAuthorController returns distinct author suggestions")]
    public async Task ExternalAuthorController_ReturnsOk_WithAuthorSuggestions()
    {
        await using var database = new TestDatabase();
        database.Context.ResourcesExternalAuthors.AddRange(
            new ResourceExternalAuthor { Id = 1, ResourceId = 1, Author = "alpha" },
            new ResourceExternalAuthor { Id = 2, ResourceId = 2, Author = "alpha" });
        await database.Context.SaveChangesAsync();
        var service = new ExternalAuthorService(database.Context,
            database.CreateGetLikeBySelector<string, string>());
        var controller = new ExternalAuthorController(service);

        var action = await controller.GetExternalAuthorsLike(string.Empty, 10);

        var result = Assert.IsType<OkObjectResult>(action.Result);
        Assert.Equal(["alpha"], Assert.IsType<List<string>>(result.Value));
    }

    [Fact(DisplayName = "UserController prefixes validation errors with its controller name")]
    public async Task UserController_ReturnsBadRequest_WhenLimitIsInvalid()
    {
        await using var database = new TestDatabase();
        var service = new UserService(database.Context,
            database.CreateGetLikeBySelector<UserInfo, SNEStorage.DTOs.UserNameDTO>());
        var controller = new UserController(service);

        var action = await controller.GetUsersLike("user", 0);

        var result = Assert.IsType<BadRequestObjectResult>(action.Result);
        Assert.Equal("UserController-InvalidMaxAmount", result.Value);
    }
}
