using System.Linq.Expressions;

namespace SNEStorage.DTOs.QueryOperations;

public record GetLikeQuery<TItem>(string? Search, int MaxAmount,
    int MaxAmountLimit, int MinimumLength = 2,
    Expression<Func<TItem, bool>>? ShortSearchPredicate = null,
    bool OrderShortSearchBySelector = true)
{
    public string NormalizedSearch => Search?.ToLower().Trim() ?? "";

    public ServiceResult Validate()
    {
        if (Search?.Length > TextLimits.SEARCH)
            return ServiceResult.InvalidSearch;

        if (MaxAmount <= 0)
            return ServiceResult.InvalidMaxAmount;

        return MaxAmount > MaxAmountLimit ?
            ServiceResult.MaxAmountSurpassTheLimit :
            ServiceResult.Success;
    }
}
