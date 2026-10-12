using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;

namespace SNEStorage.Services.Interfaces;
public interface IFullGet<TModel, TResult, TFilter, TSort, TSelect> 
    where TResult : Enum
    where TSort : struct, Enum
{
    public IQueryable<TModel> GetFilter(IQueryable<TModel> query, TFilter filter);
    public IQueryable<TModel> GetSort(IQueryable<TModel> query, TSort sort);
    public IQueryable<TSelect> GetSelect(IQueryable<TModel> query);
    public Task<OneOf<List<TSelect>, TResult>> FullGet(TFilter filter, TSort sort, int maxAmount = 10, int page = 0, CancellationToken ct = default);
    public async Task<OneOf<List<TSelect>, TResult>> FullGetResult(IQueryable<TModel> query, Func<TFilter, TSort, CancellationToken, Task<TResult>> validation, TFilter filter, TSort sort, PaginationRequest<TResult> paginationRequest, DBHelperService dbHelperService, TResult error, CancellationToken ct = default)
    {
        var validationResult = await validation(filter, sort, ct);
        if (!validationResult.Equals(paginationRequest.Success))
            return validationResult;
        return await FullGetResultEnd(query, filter, sort, paginationRequest, dbHelperService, error, ct);
    }
    public async Task<OneOf<List<TSelect>, TResult>> FullGetResult(IQueryable<TModel> query, Func<TFilter, TSort, TResult> validation, TFilter filter, TSort sort,PaginationRequest<TResult> paginationRequest, DBHelperService dbHelperService, TResult error, CancellationToken ct = default)
    {
        var validationResult = validation(filter, sort);
        if (!validationResult.Equals(paginationRequest.Success))
            return validationResult;
        return await FullGetResultEnd(query, filter, sort, paginationRequest, dbHelperService, error, ct);
    }
    private async Task<OneOf<List<TSelect>, TResult>> FullGetResultEnd(IQueryable<TModel> query, TFilter filter, TSort sort, PaginationRequest<TResult> paginationRequest, DBHelperService dbHelperService, TResult error, CancellationToken ct = default)
    {
        var validationResult = paginationRequest.IsValid(sort);
        if (!validationResult.Equals(paginationRequest.Success))
            return validationResult;
        query = GetFilter(query, filter);
        query = GetSort(query, sort);
        query = query
                .Skip(paginationRequest.Skip)
                .Take(paginationRequest.MaxAmountPerPage.Value);
        var select = GetSelect(query);

        return await dbHelperService.TryFunction(
            async cancel => await select
                .ToListAsync(cancel), error, ct: ct);
    }

}
