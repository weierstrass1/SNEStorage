namespace SNEStorage.Services.Interfaces;

public interface IFullGetDefinition<TModel, TFilter, TSort, TSelect>
{
    public IQueryable<TModel> GetFilter(IQueryable<TModel> query, TFilter filter);
    public IQueryable<TModel> GetSort(IQueryable<TModel> query, TSort sort);
    public IQueryable<TSelect> GetSelect(IQueryable<TModel> query);
}
