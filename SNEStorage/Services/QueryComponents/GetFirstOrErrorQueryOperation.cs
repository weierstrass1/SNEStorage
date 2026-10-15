using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Services.Interfaces;

namespace SNEStorage.Services.QueryComponents;

public sealed class GetFirstOrErrorQueryOperation<TItem>(DBHelperService dbHelperService,
    ILogger<GetFirstOrErrorQueryOperation<TItem>> logger) :
    ITaskQueryOperationWithTransform<TItem, ServiceResult, TItem> where TItem : class
{
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly ILogger<GetFirstOrErrorQueryOperation<TItem>> _logger = logger;

    public async Task<OneOf<TItem, ServiceResultContext>> Execute(
        IQueryable<TItem> query, ServiceResult notFound,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0, CancellationToken ct = default)
    {
        var result = await _dbHelperService.TryFunction(
            cancel => query.FirstOrDefaultAsync(cancel), ServiceResult.DBError,
            filePath: filePath, methodName: methodName, line: line, ct: ct);

        if (!result.TryPickT0(out var item, out var error))
            return error;

        return item is null ?
            notFound.Return(_logger, filePath: filePath, methodName: methodName, line: line) :
            item;
    }
}
