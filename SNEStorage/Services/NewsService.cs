using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.DTOs.QueryOperations;
using SNEStorage.Models;
using SNEStorage.Services.Interfaces;
using SNEStorage.Services.QueryComponents;

namespace SNEStorage.Services;

public enum NewsSorting
{
    RecentFirst,
    RecentLast
}

public partial class NewsService(SnestorageContext context, DBHelperService dbHelperService,
    AccountService accountService, DiscordService discordService,
    PaginatedListQueryOperation<NewsReadDTO, NewsSorting> getPage,
    GetSingleOrErrorQueryOperation<NewsReadDTO> getSingle,
    GetByPropertyQueryOperation<New, long> getById,
    ILogger<NewsService> logger) :
    IFullGet<New, NewsFilterDTO, NewsSorting, NewsReadDTO>,
    IEditSingle<long, NewsEditDTO, New>,
    ICreate<NewsCreateDTO, New>
{
    public const int MAX_AMOUNT_PER_PAGE = 50;
    public const int MAX_TITLE_LENGTH = 200;

    private readonly SnestorageContext _context = context;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly AccountService _accountService = accountService;
    private readonly DiscordService _discordService = discordService;
    private readonly PaginatedListQueryOperation<NewsReadDTO, NewsSorting> _getPage = getPage;
    private readonly GetSingleOrErrorQueryOperation<NewsReadDTO> _getSingle = getSingle;
    private readonly GetByPropertyQueryOperation<New, long> _getById = getById;
    private readonly ILogger<NewsService> _logger = logger;

    public Task<OneOf<List<NewsReadDTO>, ServiceResultContext>> FullGet(
        NewsFilterDTO filter, NewsSorting sort, int maxAmount = 10,
        int page = 0, CancellationToken ct = default)
    {
        var query = GetFilter(_context.News.AsNoTracking(), filter);
        var resultQuery = GetSelect(GetSort(query, sort));
        var parameter = new PaginatedQuery<NewsSorting>(
            new PaginationRequest(maxAmount, MAX_AMOUNT_PER_PAGE, page), sort);

        return _getPage.Execute(resultQuery, parameter, ct: ct);
    }

    public Task<OneOf<NewsReadDTO, ServiceResultContext>> Get(
        long newsId, CancellationToken ct = default)
    {
        var query = GetSelect(_context.News.AsNoTracking()
            .Where(news => news.Id == newsId));

        return _getSingle.Execute(query, ServiceResult.NewNotFound, ct: ct);
    }

    public Task<OneOf<New, ServiceResultContext>> Edit(
        long key, NewsEditDTO edit, CancellationToken ct = default)
    {
        IEditSingle<long, NewsEditDTO, New> editor = this;
        return editor.EditSingle(key, edit, ct);
    }

    public Task<OneOf<New, ServiceResultContext>> Create(
        NewsCreateDTO create, CancellationToken ct = default)
    {
        ICreate<NewsCreateDTO, New> creator = this;
        return creator.CreateItem(create, ct);
    }
}
