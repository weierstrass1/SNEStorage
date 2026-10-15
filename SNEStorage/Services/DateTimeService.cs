using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services.QueryComponents;

namespace SNEStorage.Services;

public class DateTimeService(SnestorageContext context,
    GetFirstOrErrorQueryOperation<UserInfo> getFirst,
    ILogger<DateTimeService> logger)
{
    private readonly SnestorageContext _context = context;
    private readonly GetFirstOrErrorQueryOperation<UserInfo> _getFirst = getFirst;
    private readonly ILogger<DateTimeService> _logger = logger;

    public async Task<OneOf<int, ServiceResultContext>> GetYear(
        string userId, CancellationToken ct = default)
    {
        var query = _context.UserInfos.AsNoTracking()
            .Include(info => info.TimeZone)
            .Where(info => info.UserId == userId);

        var infoResult = await _getFirst.Execute(
            query, ServiceResult.UserNotFound, ct: ct);

        if (!infoResult.TryPickT0(out var info, out var error))
            return error;

        if (info.TimeZone is null)
            return ServiceResult.UserNotFound.Return(_logger);

        return DateTime.UtcNow.AddHours(info.TimeZone.Value).Year;
    }
}
