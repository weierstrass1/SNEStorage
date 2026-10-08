using Microsoft.EntityFrameworkCore;
using SNEStorage.Models;

namespace SNEStorage.Services;
public class ExternalAuthorService(SnestorageContext context)
{
    private readonly SnestorageContext _context = context;
    public async Task<List<string>> GetAuthorLike(string likeString, int maxAmount = 10, CancellationToken ct = default)
    {
        var term = likeString?.ToLower().Trim() ?? "";
        var query = _context.ResourcesExternalAuthors.AsNoTracking();

        if (string.IsNullOrWhiteSpace(term) || term.Length < 2)
            return await query.Select(v => v.Author).Distinct().Order().ToListAsync(ct);

        var escaped = term
        .Replace(@"\", @"\\")
        .Replace("%", @"\%")
        .Replace("_", @"\_")
        .Replace("[", @"\[");

        var pattern = $"%{escaped}%";

        return await query
            .Where(v => EF.Functions.Like(v.Author, pattern, @"\"))
            .Select(v => v.Author)
            .Distinct()
            .Order()
            .Take(maxAmount)
            .ToListAsync(ct);
    }
}
