using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.DTOs.QueryOperations;
using SNEStorage.Services.Interfaces;

namespace SNEStorage.Services.QueryComponents;

public sealed class GetByIdQueryOperation<TItem, TKey>(
    GetFirstOrErrorQueryOperation<TItem> getFirst) :
    ITaskQueryOperationWithTransform<TItem, GetByIdQuery<TKey>, TItem>
    where TItem : class, IHaveId<TKey>
{
    private readonly GetFirstOrErrorQueryOperation<TItem> _getFirst = getFirst;

    public Task<OneOf<TItem, ServiceResultContext>> Execute(
        IQueryable<TItem> query, GetByIdQuery<TKey> parameter,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0, CancellationToken ct = default)
    {
        var item = Expression.Parameter(typeof(TItem), "item");
        var id = Expression.Property(item, nameof(IHaveId<TKey>.Id));
        var value = Expression.Constant(parameter.Id, typeof(TKey));
        var predicate = Expression.Lambda<Func<TItem, bool>>(Expression.Equal(id, value), item);

        return _getFirst.Execute(query.Where(predicate), parameter.NotFound,
            filePath, methodName, line, ct);
    }
}
