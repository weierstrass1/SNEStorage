using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.DTOs.QueryOperations;
using SNEStorage.Services.Interfaces;

namespace SNEStorage.Services.QueryComponents;

public sealed class GetByPropertyQueryOperation<TItem, TValue>(
    GetFirstOrErrorQueryOperation<TItem> getFirst) :
    ITaskQueryOperationWithTransform<TItem, GetByPropertyQuery<TItem, TValue>, TItem>
    where TItem : class
{
    private readonly GetFirstOrErrorQueryOperation<TItem> _getFirst = getFirst;

    public Task<OneOf<TItem, ServiceResultContext>> Execute(
        IQueryable<TItem> query, GetByPropertyQuery<TItem, TValue> parameter,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0, CancellationToken ct = default)
    {
        var value = Expression.Constant(parameter.Value, typeof(TValue));
        var predicate = Expression.Lambda<Func<TItem, bool>>(
            Expression.Equal(parameter.Selector.Body, value),
            parameter.Selector.Parameters);

        return _getFirst.Execute(query.Where(predicate), parameter.NotFound,
            filePath, methodName, line, ct);
    }
}
