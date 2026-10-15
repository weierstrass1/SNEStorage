using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using System.Runtime.CompilerServices;

namespace SNEStorage.Services.Interfaces;

public interface IFullGet<TModel, TFilter, TSort, TSelect> :
    IFullGetDefinition<TModel, TFilter, TSort, TSelect>
    where TSort : struct, Enum
{
    public Task<OneOf<List<TSelect>, ServiceResultContext>> FullGet(
        TFilter filter, TSort sort, int maxAmount = 10, int page = 0,
        CancellationToken ct = default);

    public async Task<OneOf<List<TSelect>, ServiceResultContext>> FullGetResult(
        IQueryable<TModel> query, TFilter filter, TSort sort,
        PaginationRequest pagination, DBHelperService dbHelperService,
        [CallerFilePath] string filePath = "",
        [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0, CancellationToken ct = default)
    {
        query = GetFilter(query, filter);
        query = GetSort(query, sort);
        query = query.Skip(pagination.Skip).Take(pagination.MaxAmount);

        IQueryable<TSelect> select = GetSelect(query);

        return await dbHelperService.TryFunction(
            cancel => select.ToListAsync(cancel),
            ServiceResult.DBError, filePath: filePath,
            methodName: methodName, line: line, ct: ct);
    }
}
