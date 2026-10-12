using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Services;
public class UserService(SnestorageContext context,
    DBHelperService dbHelperService)
{
    public const int MAX_AMOUNT_LIMIT = 20;
    private readonly SnestorageContext _context = context;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    public async Task<OneOf<List<UserNameDTO>,GetLikeResult>> GetUserLike(string likeString, int maxAmount = 10, CancellationToken ct = default)
    {
        if (maxAmount <= 0)
            return GetLikeResult.InvalidMaxAmount;
        if (maxAmount > MAX_AMOUNT_LIMIT)
            return GetLikeResult.MaxAmountSurpassTheLimit;

        var term = likeString?.ToLower().Trim() ?? "";
        IQueryable<UserInfo> query = _context.UserInfos
            .AsNoTracking();    

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
        return await _dbHelperService.TryFunction(
            async cancel => await query
            .OrderBy(v => v.User!.UserName)
            .Take(maxAmount)
            .Select(ui => new UserNameDTO
            {
                Id = ui.UserId,
                Username = ui.User!.UserName!,
                UsernameColor = ui.UsernameColor
            })
            .ToListAsync(cancel),
            GetLikeResult.DBError, ct: ct);
    }
}
