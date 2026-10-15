using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.DTOs.QueryOperations;
using SNEStorage.Models;

namespace SNEStorage.Services;
public partial class FlagsService
{
    public async Task<OneOf<Flag, ServiceResultContext>> CreateInternal(CommentFlagCreateDTO create, CancellationToken ct = default)
    {
        string userId = _accountService.GetUserID()!;

        CommentFlags rflag = new()
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
            CommentId = create.CommentID,
        };
        _context.CommentFlags.Add(rflag);
        var res = await _dbHelperService.TrySyncContext(ct: ct);
        return res.Result == ServiceResult.Success ?
            rflag.Flag : res;
    }
    public async Task<ServiceResultContext> ValidateCreate(CommentFlagCreateDTO create, CancellationToken ct = default)
    {
        string? userId = _accountService.GetUserID();
        if (userId == null)
            return ServiceResult.Unauthorized.Return(_logger);

        GetByPropertyQuery<Reason, long> getByProperty = new(r => r.Id, create.ReasonID,
            ServiceResult.ReasonNotFound);

        var res1 = await _getReasonById.Execute(_context.Reasons.AsNoTracking(), getByProperty);
        if (!res1.TryPickT0(out _, out var error))
            return error;

        GetByPropertyQuery<Comment, long> getCommentByProperty = new(r => r.Id, create.ReasonID,
            ServiceResult.CommentNotFound);

        var res2 = await _getCommentById.Execute(_context.Resources.AsNoTracking(), getCommentByProperty);
        if(!res2.TryPickT0(out _, out error))
             return error;

        var repeatedRes = await _dbHelperService.TryFunction(
            cancel => _context.CommentFlags
                .AnyAsync(rf => rf.CommentId == create.CommentID &&
                    rf.Flag!.UserId == userId, cancel), ct: ct);
        return repeatedRes.TryPickT0(out var isRepeated, out error) ?
            (isRepeated ? ServiceResult.RepeatedFlag : ServiceResult.Success).Return(_logger) :
            error;
    }
}
