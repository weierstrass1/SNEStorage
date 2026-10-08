using Microsoft.EntityFrameworkCore;
using SNEStorage.Models;

namespace SNEStorage.Services;
public class DateTimeService(SnestorageContext context)
{
    private readonly SnestorageContext _context = context;
    public async Task<int?> GetYear(string userId, CancellationToken ct = default)
    {
        var info = await _context.UserInfos
                    .AsNoTracking()
                    .Include(info => info.TimeZone)
                    .FirstOrDefaultAsync(info => info.UserId == userId, ct);

        if (info?.TimeZone is null)
            return null;
        return DateTime.UtcNow.AddHours(info.TimeZone!.Value).Year;
    }
}
