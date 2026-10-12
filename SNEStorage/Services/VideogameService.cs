using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.Controllers;
using SNEStorage.Models;

namespace SNEStorage.Services;
public class VideogameService(SnestorageContext context,
    DBHelperService dbHelperService)
{
    public const int MAX_AMOUNT_LIMIT = 20;
    private readonly SnestorageContext _context = context;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    public async Task<OneOf<List<Videogame>,GetLikeResult>> GetVideogamesLike(string likeString, int maxAmount = 10, CancellationToken ct = default)
    {
        if (maxAmount <= 0)
            return GetLikeResult.InvalidMaxAmount;
        if (maxAmount > MAX_AMOUNT_LIMIT)
            return GetLikeResult.MaxAmountSurpassTheLimit;

        var term = likeString?.ToLower().Trim() ?? "";
        var query = _context.Videogames.AsNoTracking();

        if(string.IsNullOrWhiteSpace(term) || term.Length < 2)
            return await _dbHelperService.TryFunction(
                async cancel => await query
                    .Where(v => v.Id == 1 || v.Id == 2)
                    .OrderBy(v => v.Id)
                    .Take(maxAmount)
                    .ToListAsync(cancel),
                GetLikeResult.DBError, ct: ct);

        var escaped = term
        .Replace(@"\", @"\\")
        .Replace("%", @"\%")
        .Replace("_", @"\_")
        .Replace("[", @"\[");

        var pattern = $"%{escaped}%";

        return await _dbHelperService.TryFunction(
                async cancel => await query
                    .Where(v => EF.Functions.Like(v.Name, pattern, @"\"))
                    .OrderBy(v => v.Name)
                    .Take(maxAmount)
                    .ToListAsync(cancel),
                GetLikeResult.DBError, ct: ct);
    }
}
