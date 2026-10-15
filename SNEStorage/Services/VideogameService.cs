using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.DTOs.QueryOperations;
using SNEStorage.Models;
using SNEStorage.Services.QueryComponents;

namespace SNEStorage.Services;

public class VideogameService(SnestorageContext context,
    GetLikeBySelectorQueryOperation<Videogame, Videogame> getLike)
{
    public const int MAX_AMOUNT_LIMIT = 20;

    private readonly SnestorageContext _context = context;
    private readonly GetLikeBySelectorQueryOperation<Videogame, Videogame> _getLike = getLike;

    public Task<OneOf<List<Videogame>, ServiceResultContext>> GetVideogamesLike(
        string likeString, int maxAmount = 10, CancellationToken ct = default)
    {
        var query = _context.Videogames.AsNoTracking()
            .OrderBy(videogame => videogame.Id);

        var parameter = new GetLikeBySelectorQuery<Videogame, Videogame>(
            likeString, maxAmount, MAX_AMOUNT_LIMIT,
            videogame => videogame.Name, videogame => videogame,
            ShortSearchPredicate: videogame => videogame.Id == 1 || videogame.Id == 2,
            OrderShortSearchBySelector: false);

        return _getLike.Execute(query, parameter, ct: ct);
    }
}
