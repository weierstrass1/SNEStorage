using Microsoft.EntityFrameworkCore;
using SNEStorage.Models;

namespace SNEStorage.Services;
public class VideogameService(SnestorageContext context)
{
    private readonly SnestorageContext _context = context;
    public async Task<List<Videogame>> GetVideogamesLike(string likeString, int maxAmount = 10, CancellationToken ct = default)
    {
        var term = likeString?.ToLower().Trim() ?? "";
        var query = _context.Videogames.AsNoTracking();

        if(string.IsNullOrWhiteSpace(term) || term.Length < 2)
            return await query.Where(v => v.Id == 1 || v.Id == 2).OrderBy(v => v.Id).ToListAsync(ct);

        var escaped = term
        .Replace(@"\", @"\\")
        .Replace("%", @"\%")
        .Replace("_", @"\_")
        .Replace("[", @"\[");

        var pattern = $"%{escaped}%";

        return await query
            .Where(v => EF.Functions.Like(v.Name, pattern, @"\"))
            .OrderBy(v => v.Name)
            .Take(maxAmount)
            .ToListAsync(ct);
    }
}
