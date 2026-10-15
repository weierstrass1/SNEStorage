using OneOf;
using SNEStorage.DTOs;
using SNEStorage.DTOs.QueryOperations;
using SNEStorage.Models;

namespace SNEStorage.Services;

public partial class FlagsService
{
    public Task<ServiceResultContext> EditInternal(Flag item, bool edit, CancellationToken ct = default)
    {
        item.Resolved = edit;

        return _dbHelperService.TrySyncContext(ct: ct);
    }
    public Task<OneOf<Flag, ServiceResultContext>> FindEdit(long key, CancellationToken ct = default)
    {
        var parameter = new GetByPropertyQuery<Flag, long>(
            news => news.Id, key, ServiceResult.NewNotFound);

        return _getById.Execute(_context.Flags, parameter, ct: ct);
    }
    public Task<ServiceResultContext> ValidateEdit(bool edit, CancellationToken ct = default)
    {
        if (!_accountService.IsModeratorOrAdmin())
            return Task.FromResult(string.IsNullOrWhiteSpace(_accountService.GetUserID()) ?
                ServiceResult.Unauthorized.Return(_logger) :
                ServiceResult.Forbidden.Return(_logger));
        return Task.FromResult(ServiceResult.Success.Return(_logger));
    }
}
