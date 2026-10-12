namespace SNEStorage.DTOs;

public class PaginationRequest<TResult> where TResult : Enum
{
    public required MaxAmountPerPage<TResult> MaxAmountPerPage { get; set; }
    public required Page<TResult> Page { get; set; }
    public required TResult SkipError { get; set; }
    public required TResult SortError { get; set; }
    public required TResult Success { get; set; }
    public int Skip => MaxAmountPerPage.Value * Page.Value;
    public TResult IsValid<TSort>(TSort sort) where TSort : struct, Enum
    {
        if (!Enum.IsDefined(sort))
            return SortError;
        if (!MaxAmountPerPage.IsValid())
            return MaxAmountPerPage.Error;
        if (!Page.IsValid())
            return Page.Error;
        long skip = (long)MaxAmountPerPage.Value * Page.Value;
        return skip > int.MaxValue ?
            SkipError : Success;
    }
}
public class MaxAmountPerPage<TResult>(int value, int maxValue, TResult error) where TResult : Enum
{
    public int Value { get; } = value;
    public int MaxValue { get; } = maxValue;
    public TResult Error { get; } = error;
    public bool IsValid()
    {
        return Value > 0 && Value <= MaxValue;
    }
}
public class Page<TResult>(int value, TResult error) where TResult : Enum
{
    public int Value { get; } = value;
    public TResult Error { get; } = error;
    public bool IsValid()
    {
        return Value >= 0;
    }
}