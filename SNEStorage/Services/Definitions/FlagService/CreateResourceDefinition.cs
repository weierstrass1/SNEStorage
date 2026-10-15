using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.DTOs.QueryOperations;
using SNEStorage.Models;

namespace SNEStorage.Services;
public partial class FlagsService
{
    public async Task<OneOf<Flag, ServiceResultContext>> CreateInternal(ResourceFlagCreateDTO create, CancellationToken ct = default)
    {
        string userId = _accountService.GetUserID()!;

        ResourceFlags rflag = new()
        {
            Id = 0,
            FlagId = 0,
            Flag = new()
            {
                Id = 0,
                ReasonId = create.ReasonID,
                CreatedAt = DateTime.UtcNow,
                UserId = userId
            },
            ResourceId = create.ResourceID,
        };
        _context.ResourcesFlags.Add(rflag);
        var res = await _dbHelperService.TrySyncContext(ct: ct);
        return res.Result == ServiceResult.Success ?
            rflag.Flag : res;
    }
    public async Task<ServiceResultContext> ValidateCreate(ResourceFlagCreateDTO create, CancellationToken ct = default)
    {
        string? userId = _accountService.GetUserID();
        if (userId == null)
            return ServiceResult.Unauthorized.Return(_logger);

        GetByPropertyQuery<Reason, long> getByProperty = new(r => r.Id, create.ReasonID,
            ServiceResult.ReasonNotFound);

        var res1 = await _getReasonById.Execute(_context.Reasons.AsNoTracking(), getByProperty);
        if (!res1.TryPickT0(out _, out var error))
            return error;

        GetByPropertyQuery<Resource, long> getResourceByProperty = new(r => r.Id, create.ReasonID,
            ServiceResult.ResourceNotFound);

        var res2 = await _getResourceById.Execute(_context.Resources.AsNoTracking(), getResourceByProperty);
        if(!res2.TryPickT0(out _, out error))
             return error;

        var repeatedRes = await _dbHelperService.TryFunction(
            cancel => _context.ResourcesFlags
                .AnyAsync(rf => rf.ResourceId == create.ResourceID &&
                    rf.Flag!.UserId == userId, cancel), ct: ct);
        return repeatedRes.TryPickT0(out var isRepeated, out error) ?
            (isRepeated ? ServiceResult.RepeatedFlag : ServiceResult.Success).Return(_logger) :
            error;
    }
}
