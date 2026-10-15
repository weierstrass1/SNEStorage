using System.Linq.Expressions;

namespace SNEStorage.DTOs.QueryOperations;

public sealed record GetLikeBySelectorQuery<TItem, TResult>(string? Search,
    int MaxAmount, int MaxAmountLimit,
    Expression<Func<TItem, string>> SearchSelector,
    Expression<Func<TItem, TResult>> ResultSelector, int MinimumLength = 2,
    Expression<Func<TItem, bool>>? ShortSearchPredicate = null,
    bool OrderShortSearchBySelector = true) :
    GetLikeQuery<TItem>(Search, MaxAmount, MaxAmountLimit, MinimumLength,
        ShortSearchPredicate, OrderShortSearchBySelector);
