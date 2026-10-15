using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Services.Interfaces;

namespace SNEStorage.Services.QueryComponents;

public sealed class GetListQueryOperation<TItem>(
    DBHelperService dbHelperService) :
    ITaskQueryOperationWithTransform<TItem, List<TItem>>
{
    private readonly DBHelperService _dbHelperService = dbHelperService;

    public Task<OneOf<List<TItem>, ServiceResultContext>> Execute(
        IQueryable<TItem> query,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0, CancellationToken ct = default)
    {
        return _dbHelperService.TryFunction(
            cancel => query.ToListAsync(cancel), ServiceResult.DBError,
            filePath: filePath, methodName: methodName, line: line, ct: ct);
    }
}
