using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Services;

public class VideogameService(
    SnestorageContext context,
    DBHelperService dbHelperService,
    ILogger<VideogameService> logger)
{
    public const int MAX_AMOUNT_LIMIT = 20;

    private readonly SnestorageContext _context = context;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly ILogger<VideogameService> _logger = logger;

    public async Task<OneOf<List<Videogame>, ServiceResultContext>> GetVideogamesLike(
        string likeString, int maxAmount = 10, CancellationToken ct = default)
    {
        if (maxAmount <= 0)
            return ServiceResult.InvalidMaxAmount.Return(_logger);

        if (maxAmount > MAX_AMOUNT_LIMIT)
            return ServiceResult.MaxAmountSurpassTheLimit.Return(_logger);

        string term = likeString?.ToLower().Trim() ?? "";
        IQueryable<Videogame> query = _context.Videogames.AsNoTracking();
        IQueryable<Videogame> resultQuery;

        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
        {
            resultQuery = query.Where(videogame =>
                videogame.Id == 1 || videogame.Id == 2)
                .OrderBy(videogame => videogame.Id);
        }
        else
        {
            string escaped = term.Replace(@"\", @"\\").Replace("%", @"\%")
                .Replace("_", @"\_").Replace("[", @"\[");

            string pattern = $"%{escaped}%";

            resultQuery = query.Where(videogame =>
                EF.Functions.Like(videogame.Name, pattern, @"\"))
                .OrderBy(videogame => videogame.Name);
        }

        resultQuery = resultQuery.Take(maxAmount);

        return await _dbHelperService.TryFunction(
            cancel => resultQuery.ToListAsync(cancel),
            ServiceResult.DBError, ct: ct);
    }
}
