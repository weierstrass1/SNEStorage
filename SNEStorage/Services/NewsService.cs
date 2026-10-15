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

public class NewsService(SnestorageContext context, DBHelperService dbHelperService,
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

    public IQueryable<New> GetFilter(
        IQueryable<New> query, NewsFilterDTO filter) => query;

    public IQueryable<NewsReadDTO> GetSelect(IQueryable<New> query)
    {
        return query.Select(news => new NewsReadDTO
        {
            Id = news.Id,
            CreatedAt = news.CreatedAt,
            Title = news.Title,
            Text = news.Text,
            UserSummary = new UserNameDTO
            {
                Id = news.PosterUserId,
                Username = news.PosterUser!.UserName!,
                UsernameColor = news.PosterUser.UserInfo!.UsernameColor,
                Avatar = new FileResponse
                {
                    Id = news.PosterUser.UserInfo.AvatarId,
                    FileTypeId = news.PosterUser.UserInfo.Avatar!.FileTypeId,
                    URL = news.PosterUser.UserInfo.Avatar.URL
                }
            }
        });
    }

    public IQueryable<New> GetSort(IQueryable<New> query, NewsSorting sort)
    {
        return sort switch
        {
            NewsSorting.RecentLast => query
                .OrderBy(news => news.CreatedAt)
                .ThenBy(news => news.Id),

            _ => query
                .OrderByDescending(news => news.CreatedAt)
                .ThenByDescending(news => news.Id)
        };
    }

    public Task<OneOf<New, ServiceResultContext>> Edit(
        long key, NewsEditDTO edit, CancellationToken ct = default)
    {
        IEditSingle<long, NewsEditDTO, New> editor = this;
        return editor.EditSingle(key, edit, ct);
    }

    public Task<ServiceResultContext> ValidateEdit(
        NewsEditDTO edit, CancellationToken ct = default)
    {
        if (!_accountService.IsModeratorOrAdmin())
            return Task.FromResult(string.IsNullOrWhiteSpace(_accountService.GetUserID()) ?
                ServiceResult.Unauthorized.Return(_logger) :
                ServiceResult.Forbidden.Return(_logger));

        if (edit.Title is not null &&
            (string.IsNullOrWhiteSpace(edit.Title) ||
            edit.Title.Trim().Length > MAX_TITLE_LENGTH))
            return Task.FromResult(ServiceResult.InvalidTitle.Return(_logger));

        return Task.FromResult(edit.Text is not null &&
            string.IsNullOrWhiteSpace(edit.Text) ?
                ServiceResult.InvalidText.Return(_logger) :
                ServiceResult.Success.Return(_logger));
    }

    public Task<OneOf<New, ServiceResultContext>> FindEdit(
        long key, CancellationToken ct = default)
    {
        var parameter = new GetByPropertyQuery<New, long>(
            news => news.Id, key, ServiceResult.NewNotFound);

        return _getById.Execute(_context.News, parameter, ct: ct);
    }

    public async Task<ServiceResultContext> EditInternal(
        New item, NewsEditDTO edit, CancellationToken ct = default)
    {
        item.Title = edit.Title?.Trim() ?? item.Title;
        item.Text = edit.Text?.Trim() ?? item.Text;

        return await _dbHelperService.TrySyncContext(ct: ct);
    }

    public Task<OneOf<New, ServiceResultContext>> Create(
        NewsCreateDTO create, CancellationToken ct = default)
    {
        ICreate<NewsCreateDTO, New> creator = this;
        return creator.CreateItem(create, ct);
    }

    public Task<ServiceResultContext> ValidateCreate(
        NewsCreateDTO create, CancellationToken ct = default)
    {
        if (!_accountService.IsModeratorOrAdmin())
            return Task.FromResult(string.IsNullOrWhiteSpace(_accountService.GetUserID()) ?
                ServiceResult.Unauthorized.Return(_logger) :
                ServiceResult.Forbidden.Return(_logger));

        if (string.IsNullOrWhiteSpace(create.Title) ||
            create.Title.Trim().Length > MAX_TITLE_LENGTH)
            return Task.FromResult(ServiceResult.InvalidTitle.Return(_logger));

        return Task.FromResult(string.IsNullOrWhiteSpace(create.Text) ?
            ServiceResult.InvalidText.Return(_logger) :
            ServiceResult.Success.Return(_logger));
    }

    public async Task<OneOf<New, ServiceResultContext>> CreateInternal(
        NewsCreateDTO create, CancellationToken ct = default)
    {
        string? userId = _accountService.GetUserID();

        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult.Unauthorized.Return(_logger);

        var news = new New
        {
            Id = 0,
            PosterUserId = userId,
            CreatedAt = DateTime.UtcNow,
            Title = create.Title.Trim(),
            Text = create.Text.Trim()
        };

        _context.News.Add(news);

        var saveResult = await _dbHelperService.TrySyncContext(news, ct: ct);

        if (!saveResult.TryPickT0(out var newItem, out var saveError))
            return saveError;

        DiscordBotEventDTO notification = new (Guid.NewGuid(),
            DiscordBotEventTypes.NewsPublished, newItem.Id.ToString(),
            DateTimeOffset.UtcNow);

        _ = await _discordService.NotifyBotAsync(notification, ct);
        return newItem;
    }
}
