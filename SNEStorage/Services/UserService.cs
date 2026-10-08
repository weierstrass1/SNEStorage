using Microsoft.EntityFrameworkCore;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Services;
public class UserService(SnestorageContext context)
{
    private readonly SnestorageContext _context = context;
    public async Task<List<UserNameDTO>> GetUserLike(string likeString, int maxAmount = 10, CancellationToken ct = default)
    {
        var term = likeString?.ToLower().Trim() ?? "";
        IQueryable<UserInfo> query = _context.UserInfos
            .AsNoTracking()
            .Include(ui => ui.Avatar)
            .Include(ui => ui.User);           

        if (!string.IsNullOrWhiteSpace(term) && term.Length >= 2)
        {
            var escaped = term
                .Replace(@"\", @"\\")
                .Replace("%", @"\%")
                .Replace("_", @"\_")
                .Replace("[", @"\[");

            var pattern = $"%{escaped}%";

            query = query
                .Where(v => EF.Functions.Like(v.User!.UserName, pattern, @"\"));
        }
        var res = await query
            .OrderBy(v => v.User!.UserName)
            .Take(maxAmount)
            .ToListAsync(ct);
        return [.. res.Select(ui => UserNameDTO.FromUserInfo(ui))];
    }
}
