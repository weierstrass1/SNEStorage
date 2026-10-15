using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.DTOs.QueryOperations;
using SNEStorage.Models;
using SNEStorage.Services.QueryComponents;

namespace SNEStorage.Services;

public class ExternalAuthorService(SnestorageContext context,
    GetLikeBySelectorQueryOperation<string, string> getLike)
{
    public const int MAX_AMOUNT_LIMIT = 20;

    private readonly SnestorageContext _context = context;
    private readonly GetLikeBySelectorQueryOperation<string, string> _getLike = getLike;

    public Task<OneOf<List<string>, ServiceResultContext>> GetAuthorLike(
        string likeString, int maxAmount = 10, CancellationToken ct = default)
    {
        var query = _context.ResourcesExternalAuthors.AsNoTracking()
            .Select(author => author.Author)
            .Distinct();

        var parameter = new GetLikeBySelectorQuery<string, string>(
            likeString, maxAmount, MAX_AMOUNT_LIMIT,
            author => author, author => author);

        return _getLike.Execute(query, parameter, ct: ct);
    }
}
