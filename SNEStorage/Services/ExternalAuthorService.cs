using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Services;

public class ExternalAuthorService(
    SnestorageContext context,
    DBHelperService dbHelperService,
    ILogger<ExternalAuthorService> logger)
{
    public const int MAX_AMOUNT_LIMIT = 20;

    private readonly SnestorageContext _context = context;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly ILogger<ExternalAuthorService> _logger = logger;

    public async Task<OneOf<List<string>, ServiceResultContext>> GetAuthorLike(
        string likeString, int maxAmount = 10, CancellationToken ct = default)
    {
        if (maxAmount <= 0)
            return ServiceResult.InvalidMaxAmount.Return(_logger);

        if (maxAmount > MAX_AMOUNT_LIMIT)
            return ServiceResult.MaxAmountSurpassTheLimit.Return(_logger);

        string term = likeString?.ToLower().Trim() ?? "";
        IQueryable<ResourceExternalAuthor> query =
            _context.ResourcesExternalAuthors.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(term) && term.Length >= 2)
        {
            string escaped = term.Replace(@"\", @"\\").Replace("%", @"\%")
                .Replace("_", @"\_").Replace("[", @"\[");

            string pattern = $"%{escaped}%";

            query = query.Where(author =>
                EF.Functions.Like(author.Author, pattern, @"\"));
        }

        var resultQuery = query.Select(author => author.Author)
            .Distinct()
            .Order()
            .Take(maxAmount);

        return await _dbHelperService.TryFunction(
            cancel => resultQuery.ToListAsync(cancel),
            ServiceResult.DBError, ct: ct);
    }
}
