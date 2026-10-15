using System.Runtime.CompilerServices;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.DTOs.QueryOperations;
using SNEStorage.Services.Interfaces;

namespace SNEStorage.Services.QueryComponents;

public sealed class PaginatedListQueryOperation<TItem, TSort>(
    GetListQueryOperation<TItem> getList,
    ILogger<PaginatedListQueryOperation<TItem, TSort>> logger) :
    ITaskQueryOperationWithTransform<TItem, PaginatedQuery<TSort>, List<TItem>>
    where TSort : struct, Enum
{
    private readonly GetListQueryOperation<TItem> _getList = getList;
    private readonly ILogger<PaginatedListQueryOperation<TItem, TSort>> _logger = logger;

    public async Task<OneOf<List<TItem>, ServiceResultContext>> Execute(
        IQueryable<TItem> query, PaginatedQuery<TSort> parameter,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0, CancellationToken ct = default)
    {
        ServiceResult validation = parameter.Pagination.Validate(parameter.Sort);

        if (validation != ServiceResult.Success)
            return validation.Return(_logger, filePath: filePath,
                methodName: methodName, line: line);

        var resultQuery = query.Skip(parameter.Pagination.Skip)
            .Take(parameter.Pagination.MaxAmount);

        return await _getList.Execute(resultQuery, filePath: filePath,
            methodName: methodName, line: line, ct: ct);
    }
}
