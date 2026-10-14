using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Services;

public class DateTimeService(SnestorageContext context, DBHelperService dbHelperService,
    ILogger<DateTimeService> logger)
{
    private readonly SnestorageContext _context = context;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly ILogger<DateTimeService> _logger = logger;

    public async Task<OneOf<int, ServiceResultContext>> GetYear(
        string userId, CancellationToken ct = default)
    {
        var query = _context.UserInfos.AsNoTracking()
            .Include(info => info.TimeZone)
            .Where(info => info.UserId == userId);

        var infoResult = await _dbHelperService.TryFunction(
            cancel => query.FirstOrDefaultAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (!infoResult.TryPickT0(out var info, out var queryError))
            return queryError;

        if (info?.TimeZone is null)
            return ServiceResult.UserNotFound.Return(_logger);

        return DateTime.UtcNow.AddHours(info.TimeZone.Value).Year;
    }
}
