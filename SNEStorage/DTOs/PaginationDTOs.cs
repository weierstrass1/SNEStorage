namespace SNEStorage.DTOs;

public sealed class PaginationRequest(int maxAmount, int maxAmountLimit, int page)
{
    public int MaxAmount { get; } = maxAmount;
    public int Page { get; } = page;
    public int Skip => MaxAmount * Page;

    public ServiceResult Validate<TSort>(TSort sort) where TSort : struct, Enum
    {
        if (!Enum.IsDefined(sort))
            return ServiceResult.InvalidSort;

        if (MaxAmount <= 0 || MaxAmount > maxAmountLimit)
            return ServiceResult.InvalidMaxAmount;

        if (Page < 0)
            return ServiceResult.InvalidPage;

        return (long)MaxAmount * Page > int.MaxValue ?
            ServiceResult.SkipOutOfRange : ServiceResult.Success;
    }
}
