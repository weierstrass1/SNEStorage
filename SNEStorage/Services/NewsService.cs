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
    GetFirstOrDefaultQueryOperation<LikeUsers> getLike,
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
    private readonly GetFirstOrDefaultQueryOperation<LikeUsers> _getLike = getLike;
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
    public async Task<OneOf<LikeUsers, ServiceResultContext>> AddEditOrDeleteLike(
        long newId, LikeValues value, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(value))
            return ServiceResult.UndefinedLike.Return(_logger);

        var newsRes = await Get(newId, ct);

        if (!newsRes.TryPickT0(out var news, out var error))
            return error;

        string? userId = _accountService.GetUserID();

        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult.Unauthorized.Return(_logger);

        var likeQuery = _context.LikesUsers
            .Where(like =>
                like.UserId == userId &&
                like.ScoreId == news.ScoreId);

        var likeResult = await _getLike.Execute(likeQuery, ct: ct);

        if (!likeResult.TryPickT0(out var like, out var likeError))
            return likeError;

        if (value == LikeValues.Remove)
        {
            if (like is null)
                return ServiceResult.LikeNotFound.Return(_logger);

            _context.LikesUsers.Remove(like);
            return await _dbHelperService.TrySyncContext(like, ct: ct);
        }

        if (like is null)
        {
            like = new LikeUsers
            {
                Id = 0,
                UserId = userId,
                ScoreId = news.ScoreId
            };

            _context.LikesUsers.Add(like);
        }

        like.Value = value == LikeValues.Like;
        return await _dbHelperService.TrySyncContext(like, ct: ct);
    }
}
