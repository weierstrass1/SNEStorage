using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Services.Interfaces;

namespace SNEStorage.Services.QueryComponents;

public sealed class GetFirstOrDefaultQueryOperation<TItem>(
    DBHelperService dbHelperService) :
    ITaskQueryOperationWithTransform<TItem, TItem?> where TItem : class
{
    private readonly DBHelperService _dbHelperService = dbHelperService;

    public Task<OneOf<TItem?, ServiceResultContext>> Execute(
        IQueryable<TItem> query,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0, CancellationToken ct = default)
    {
        return _dbHelperService.TryFunction(
            cancel => query.FirstOrDefaultAsync(cancel), ServiceResult.DBError,
            filePath: filePath, methodName: methodName, line: line, ct: ct);
    }
}
