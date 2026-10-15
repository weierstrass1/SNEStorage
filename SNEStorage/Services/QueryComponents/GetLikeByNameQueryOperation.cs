using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.DTOs.QueryOperations;
using SNEStorage.Services.Interfaces;

namespace SNEStorage.Services.QueryComponents;

public sealed class GetLikeByNameQueryOperation<TItem>(DBHelperService dbHelperService,
    ILogger<GetLikeByNameQueryOperation<TItem>> logger) :
    ITaskQueryOperationWithTransform<TItem, GetLikeQuery<TItem>, List<TItem>>
    where TItem : class, IHaveName
{
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly ILogger<GetLikeByNameQueryOperation<TItem>> _logger = logger;

    public async Task<OneOf<List<TItem>, ServiceResultContext>> Execute(
        IQueryable<TItem> query, GetLikeQuery<TItem> parameter,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0, CancellationToken ct = default)
    {
        ServiceResult validation = parameter.Validate();

        if (validation != ServiceResult.Success)
            return validation.Return(_logger, filePath: filePath,
                methodName: methodName, line: line);

        var item = Expression.Parameter(typeof(TItem), "item");
        var name = Expression.Call(typeof(EF), nameof(EF.Property), [typeof(string)],
            item, Expression.Constant(nameof(IHaveName.Name)));
        var selector = Expression.Lambda<Func<TItem, string>>(name, item);

        query = LikeQueryHelper.Apply(query, selector, parameter)
            .Take(parameter.MaxAmount);

        return await _dbHelperService.TryFunction(cancel => query.ToListAsync(cancel),
            ServiceResult.DBError, filePath: filePath, methodName: methodName,
            line: line, ct: ct);
    }
}
