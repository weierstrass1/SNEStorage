using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.Models;

namespace SNEStorage.Services;
public enum GetLikeResult
{
    Success,
    DBError,
    InvalidMaxAmount,
    MaxAmountSurpassTheLimit
}
public class ExternalAuthorService(SnestorageContext context,
    DBHelperService dbHelperService)
{
    public const int MAX_AMOUNT_LIMIT = 20;
    private readonly SnestorageContext _context = context;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    public async Task<OneOf<List<string>, GetLikeResult>> GetAuthorLike(string likeString, int maxAmount = 10, CancellationToken ct = default)
    {
        if (maxAmount <= 0)
            return GetLikeResult.InvalidMaxAmount;
        if (maxAmount > MAX_AMOUNT_LIMIT)
            return GetLikeResult.MaxAmountSurpassTheLimit;

        var term = likeString?.ToLower().Trim() ?? "";
        var query = _context.ResourcesExternalAuthors.AsNoTracking();

        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            return await _dbHelperService.TryFunction(
                async cancel => await query
                    .Select(v => v.Author)
                    .Distinct()
                    .Order()
                    .Take(maxAmount)
                    .ToListAsync(cancel), 
                GetLikeResult.DBError, ct:ct);

        var escaped = term
        .Replace(@"\", @"\\")
        .Replace("%", @"\%")
        .Replace("_", @"\_")
        .Replace("[", @"\[");

        var pattern = $"%{escaped}%";

        return await _dbHelperService.TryFunction(
                async cancel => await query
                .Where(v => EF.Functions.Like(v.Author, pattern, @"\"))
                .Select(v => v.Author)
                .Distinct()
                .Order()
                .Take(maxAmount)
                .ToListAsync(cancel),
                GetLikeResult.DBError, ct: ct);
    }
}
