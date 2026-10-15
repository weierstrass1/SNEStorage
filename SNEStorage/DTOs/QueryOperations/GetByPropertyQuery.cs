using System.Linq.Expressions;
using SNEStorage.DTOs;

namespace SNEStorage.DTOs.QueryOperations;

public sealed record GetByPropertyQuery<TItem, TValue>(
    Expression<Func<TItem, TValue>> Selector, TValue Value, ServiceResult NotFound);
