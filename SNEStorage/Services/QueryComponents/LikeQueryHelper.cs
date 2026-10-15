using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SNEStorage.DTOs.QueryOperations;

namespace SNEStorage.Services.QueryComponents;

internal static class LikeQueryHelper
{
    public static IQueryable<TItem> Apply<TItem>(IQueryable<TItem> query,
        Expression<Func<TItem, string>> selector, GetLikeQuery<TItem> parameter)
    {
        string search = parameter.NormalizedSearch;
        bool fullSearch = search.Length >= parameter.MinimumLength;

        if (fullSearch)
            query = query.Where(CreatePredicate(selector, search));
        else if (parameter.ShortSearchPredicate is not null)
            query = query.Where(parameter.ShortSearchPredicate);

        return fullSearch || parameter.OrderShortSearchBySelector ?
            query.OrderBy(selector) :
            query;
    }

    private static Expression<Func<TItem, bool>> CreatePredicate<TItem>(
        Expression<Func<TItem, string>> selector, string search)
    {
        string escaped = search.Replace(@"\", @"\\").Replace("%", @"\%")
            .Replace("_", @"\_").Replace("[", @"\[");

        string pattern = $"%{escaped}%";
        Expression<Func<string, bool>> like = value =>
            EF.Functions.Like(value, pattern, @"\");

        var body = new ExpressionReplaceVisitor(
            like.Parameters[0], selector.Body).Visit(like.Body)!;

        return Expression.Lambda<Func<TItem, bool>>(body, selector.Parameters);
    }
}
