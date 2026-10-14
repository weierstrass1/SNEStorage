using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;

namespace SNEStorage.Services.Interfaces;
public interface IQueryOperation
{
}
public interface IQueryOperationWithTransform<TItem, TResult> : IQueryOperation
{
    public OneOf<IQueryable<TResult>, ServiceResultContext> Execute(IQueryable<TItem> query);
}
public interface IQueryOperationWithTransform<TItem, TParam, TResult> : IQueryOperation
{
    public OneOf<IQueryable<TResult>, ServiceResultContext> Execute(IQueryable<TItem> query, TParam parameter);
}
public interface ITransform<TItem, TResult> : IQueryOperation
{
    public OneOf<TResult, ServiceResultContext> Execute(TItem item);
}
public interface ITransform<TItem, TParameter, TResult> : IQueryOperation
{
    public OneOf<TResult, ServiceResultContext> Execute(TItem item, TParameter parameter);
}
public interface ISetOperationWithTransformation<TItem, TParameter, TResult> : IQueryOperation
    where TItem : class
{
    public OneOf<TResult, ServiceResultContext> Execute(DbSet<TItem> set, TParameter parameter);
}
public interface ISetOperationWithTransformation<TItem, TResult> : IQueryOperation
    where TItem : class
{
    public OneOf<TResult, ServiceResultContext> Execute(DbSet<TItem> set);
}
public interface IActionQueryOperation<TItem> : IQueryOperation
{
    public ServiceResultContext Execute(IQueryable<TItem> set);
}
public interface IActionQueryOperation<TItem, TParam> : IQueryOperation
{
    public ServiceResultContext Execute(IQueryable<TItem> set, TParam parameter);
}
public interface IActionSetOperation<TItem> : IQueryOperation
    where TItem : class
{
    public ServiceResultContext Execute(DbSet<TItem> set);
}
public interface IActionSetOperation<TItem, TParam> : IQueryOperation
    where TItem : class
{
    public ServiceResultContext Execute(DbSet<TItem> set, TParam parameter);
}
public interface IActionOperation<TItem> : IQueryOperation
{
    public ServiceResultContext Execute(TItem set);
}
public interface IActionOperation<TItem, TParam> : IQueryOperation
{
    public ServiceResultContext Execute(TItem set, TParam parameter);
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
