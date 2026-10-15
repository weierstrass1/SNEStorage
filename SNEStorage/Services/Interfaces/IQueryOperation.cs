using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using System.Runtime.CompilerServices;

namespace SNEStorage.Services.Interfaces;
public interface IQueryOperation
{
}
public interface IQueryOperationWithTransform<TItem, TResult> : IQueryOperation
{
    public OneOf<IQueryable<TResult>, ServiceResultContext> Execute(IQueryable<TItem> query,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0);
}
public interface IQueryOperationWithTransform<TItem, TParam, TResult> : IQueryOperation
{
    public OneOf<IQueryable<TResult>, ServiceResultContext> Execute(IQueryable<TItem> query, TParam parameter,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0);
}
public interface ITransform<TItem, TResult> : IQueryOperation
{
    public OneOf<TResult, ServiceResultContext> Execute(TItem item,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0);
}
public interface ITransform<TItem, TParameter, TResult> : IQueryOperation
{
    public OneOf<TResult, ServiceResultContext> Execute(TItem item, TParameter parameter,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0);
}
public interface ISetOperationWithTransformation<TItem, TParameter, TResult> : IQueryOperation
    where TItem : class
{
    public Task<OneOf<TResult, ServiceResultContext>> Execute(DbSet<TItem> set, TParameter parameterm,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0, CancellationToken ct = default);
}
public interface ISetOperationWithTransformation<TItem, TResult> : IQueryOperation
    where TItem : class
{
    public Task<OneOf<TResult, ServiceResultContext>> Execute(DbSet<TItem> set,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0, CancellationToken ct = default);
}
public interface IActionQueryOperation<TItem> : IQueryOperation
{
    public ServiceResultContext Execute(IQueryable<TItem> set,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0);
}
public interface IActionQueryOperation<TItem, TParam> : IQueryOperation
{
    public ServiceResultContext Execute(IQueryable<TItem> set, TParam parameter,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0);
}
public interface IActionSetOperation<TItem> : IQueryOperation
    where TItem : class
{
    public Task<ServiceResultContext> Execute(DbSet<TItem> set,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0, CancellationToken ct = default);
}
public interface IActionSetOperation<TItem, TParam> : IQueryOperation
    where TItem : class
{
    public Task<ServiceResultContext> Execute(DbSet<TItem> set, TParam parameter,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0, CancellationToken ct = default);
}
public interface IActionOperation<TItem> : IQueryOperation
{
    public ServiceResultContext Execute(TItem set,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0);
}
public interface IActionOperation<TItem, TParam> : IQueryOperation
{
    public ServiceResultContext Execute(TItem set, TParam parameter,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0);
}
public interface ITaskQueryOperationWithTransform<TItem, TResult> : IQueryOperation
{
    Task<OneOf<TResult, ServiceResultContext>> Execute(IQueryable<TItem> query,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0, CancellationToken ct = default);
}
public interface ITaskQueryOperationWithTransform<TItem, TParam, TResult> : IQueryOperation
{
    Task<OneOf<TResult, ServiceResultContext>> Execute(IQueryable<TItem> query,
        TParam parameter, [CallerFilePath] string filePath = "",
        [CallerMemberName] string methodName = "", [CallerLineNumber] int line = 0,
        CancellationToken ct = default);
}
public interface IQueryOperation<TItem> : IQueryOperationWithTransform<TItem, TItem>
{
}
public interface IQueryOperation<TItem, TParam> : IQueryOperationWithTransform<TItem, TParam, TItem>
{
}
public interface ISetOperation<TItem> : ISetOperationWithTransformation<TItem, TItem>
    where TItem : class
{
}
public interface ISetOperation<TItem, TParam> : IQueryOperationWithTransform<TItem, TParam, TItem>
    where TItem : class
{
}
public interface IModify<TItem> : ITransform<TItem, TItem>
{
}
public interface IModify<TItem, TParam> : ITransform<TItem, TParam>
{
}
