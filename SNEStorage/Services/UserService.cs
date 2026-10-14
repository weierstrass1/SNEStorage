using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Services;

public class UserService(SnestorageContext context, DBHelperService dbHelperService,
    ILogger<UserService> logger)
{
    public const int MAX_AMOUNT_LIMIT = 20;

    private readonly SnestorageContext _context = context;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly ILogger<UserService> _logger = logger;

    public async Task<OneOf<List<UserNameDTO>, ServiceResultContext>> GetUserLike(
        string likeString, int maxAmount = 10, CancellationToken ct = default)
    {
        if (maxAmount <= 0)
            return ServiceResult.InvalidMaxAmount.Return(_logger);

        if (maxAmount > MAX_AMOUNT_LIMIT)
            return ServiceResult.MaxAmountSurpassTheLimit.Return(_logger);

        string term = likeString?.ToLower().Trim() ?? "";
        IQueryable<UserInfo> query = _context.UserInfos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(term) && term.Length >= 2)
        {
            string escaped = term.Replace(@"\", @"\\").Replace("%", @"\%")
                .Replace("_", @"\_").Replace("[", @"\[");

            string pattern = $"%{escaped}%";

            query = query.Where(user =>
                EF.Functions.Like(user.User!.UserName!, pattern, @"\"));
        }

        var resultQuery = query.OrderBy(user => user.User!.UserName)
            .Take(maxAmount)
            .Select(user => new UserNameDTO
            {
                Id = user.UserId,
                Username = user.User!.UserName!,
                UsernameColor = user.UsernameColor
            });

        return await _dbHelperService.TryFunction(
            cancel => resultQuery.ToListAsync(cancel),
            ServiceResult.DBError, ct: ct);
    }
}
