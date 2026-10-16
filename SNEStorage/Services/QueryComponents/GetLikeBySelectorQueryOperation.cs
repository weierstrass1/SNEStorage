using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.DTOs.QueryOperations;
using SNEStorage.Services.Interfaces;

namespace SNEStorage.Services.QueryComponents;

public sealed class GetLikeBySelectorQueryOperation<TItem, TResult>(
    DBHelperService dbHelperService,
    ILogger<GetLikeBySelectorQueryOperation<TItem, TResult>> logger) :
    ITaskQueryOperationWithTransform<TItem,
        GetLikeBySelectorQuery<TItem, TResult>, List<TResult>>
{
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly ILogger<GetLikeBySelectorQueryOperation<TItem, TResult>> _logger = logger;

    public async Task<OneOf<List<TResult>, ServiceResultContext>> Execute(
        IQueryable<TItem> query, GetLikeBySelectorQuery<TItem, TResult> parameter,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0, CancellationToken ct = default)
    {
        ServiceResult validation = parameter.Validate();

        if (validation != ServiceResult.Success)
            return validation.Return(_logger, filePath: filePath,
                methodName: methodName, line: line);

        if (parameter.Page < 0)
            return ServiceResult.NegativePage.Return(_logger,
                filePath: filePath, methodName: methodName, line: line);

        long skip = (long)parameter.MaxAmount * parameter.Page;
        if (skip > int.MaxValue)
            return ServiceResult.SkipOutOfRange.Return(_logger,
                filePath: filePath, methodName: methodName, line: line);

        var resultQuery = LikeQueryHelper.Apply(query, parameter.SearchSelector, parameter)
            .Skip((int)skip)
            .Take(parameter.MaxAmount)
            .Select(parameter.ResultSelector);

        return await _dbHelperService.TryFunction(cancel => resultQuery.ToListAsync(cancel),
            ServiceResult.DBError, filePath: filePath, methodName: methodName,
            line: line, ct: ct);
    }
}
