using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services.Interfaces;

namespace SNEStorage.Services;

public enum NewsSorting
{
    RecentFirst,
    RecentLast
}

public class NewsService(SnestorageContext context, DBHelperService dbHelperService,
    AccountService accountService, DiscordService discordService,
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
    private readonly ILogger<NewsService> _logger = logger;

    public async Task<OneOf<List<NewsReadDTO>, ServiceResultContext>> FullGet(
        NewsFilterDTO filter, NewsSorting sort, int maxAmount = 10,
        int page = 0, CancellationToken ct = default)
    {
        IFullGet<New, NewsFilterDTO, NewsSorting, NewsReadDTO> fullGet = this;
        PaginationRequest pagination = new(maxAmount, MAX_AMOUNT_PER_PAGE, page);
        ServiceResult validation = pagination.Validate(sort);

        if (validation != ServiceResult.Success)
            return validation.Return(_logger);

        return await fullGet.FullGetResult(_context.News.AsNoTracking(), filter,
            sort, pagination, _dbHelperService, ct: ct);
    }

    public async Task<OneOf<NewsReadDTO, ServiceResultContext>> Get(
        long newsId, CancellationToken ct = default)
    {
        var query = GetSelect(_context.News.AsNoTracking()
            .Where(news => news.Id == newsId));

        var result = await _dbHelperService.TryFunction(
            cancel => query.SingleOrDefaultAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (!result.TryPickT0(out var news, out var error))
            return error;

        return news is null ? ServiceResult.NewNotFound.Return(_logger) : news;
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

    public async Task<OneOf<New, ServiceResultContext>> FindEdit(
        long key, CancellationToken ct = default)
    {
        var found = await _dbHelperService.TryFunction(
            cancel => _context.News.FirstOrDefaultAsync(
                news => news.Id == key, cancel),
            ServiceResult.DBError, ct: ct);

        if (!found.TryPickT0(out var news, out var error))
            return error;

        return news is null ? ServiceResult.NewNotFound.Return(_logger) : news;
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

        var notification = new DiscordBotEventDTO(Guid.NewGuid(),
            DiscordBotEventTypes.NewsPublished, newItem.Id.ToString(),
            DateTimeOffset.UtcNow);

        _ = await _discordService.NotifyBotAsync(notification, ct);
        return newItem;
    }
}
