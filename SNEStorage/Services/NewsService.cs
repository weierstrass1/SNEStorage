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
public enum NewsServiceResult
{
    Success,
    InvalidText,
    Unauthorized,
    UserNotFound,
    NewNotFound,
    DBError,
    InvalidSort,
    InvalidMaxAmount,
    InvalidPage,
    SkipOutOfRange,
    InvalidTitle
}
public class NewsService(SnestorageContext context,
    DBHelperService dbHelperService,
    AccountService accountService) :
    IFullGet<New, NewsServiceResult, NewsFilterDTO, NewsSorting, NewsReadDTO>,
    IEditSingle<long, NewsEditDTO, New, NewsServiceResult>,
    ICreate<NewsCreateDTO, New, NewsServiceResult>
{
    public const int MAX_AMOUNT_PER_PAGE = 50;
    public const int MAX_TITLE_LENGTH = 200;
    private readonly SnestorageContext _context = context;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly AccountService _accountService = accountService;

    public async Task<OneOf<List<NewsReadDTO>, NewsServiceResult>> FullGet(
        NewsFilterDTO filter, NewsSorting sort, int maxAmount = 10,
        int page = 0, CancellationToken ct = default)
    {
        IFullGet<New, NewsServiceResult, NewsFilterDTO, NewsSorting, NewsReadDTO> fullGet = this;

        PaginationRequest<NewsServiceResult> paginationRequest = new()
        {
            MaxAmountPerPage = new(maxAmount, MAX_AMOUNT_PER_PAGE,
                NewsServiceResult.InvalidMaxAmount),
            Page = new(page, NewsServiceResult.InvalidPage),
            SkipError = NewsServiceResult.SkipOutOfRange,
            SortError = NewsServiceResult.InvalidSort,
            Success = NewsServiceResult.Success
        };

        return await fullGet.FullGetResult(_context.News.AsNoTracking(),
            (_, _) => NewsServiceResult.Success,
            filter, sort, paginationRequest, _dbHelperService,
            NewsServiceResult.DBError, ct);
    }
    public IQueryable<New> GetFilter(IQueryable<New> query, NewsFilterDTO filter) => query;
    public IQueryable<NewsReadDTO> GetSelect(IQueryable<New> query)
    {
        return query.Select(n => new NewsReadDTO
        {
            Id = n.Id,
            CreatedAt = n.CreatedAt,
            Title = n.Title,
            Text = n.Text,
            UserSummary = new UserNameDTO
            {
                Id = n.PosterUserId,
                Username = n.PosterUser!.UserName!,
                UsernameColor = n.PosterUser.UserInfo!.UsernameColor,
                Avatar = new FileResponse
                {
                    Id = n.PosterUser.UserInfo.AvatarId,
                    FileTypeId = n.PosterUser.UserInfo.Avatar!.FileTypeId,
                    URL = n.PosterUser.UserInfo.Avatar.URL
                }
            }
        });
    }
    public IQueryable<New> GetSort(IQueryable<New> query, NewsSorting sort)
    {
        return sort switch
        {
            NewsSorting.RecentLast => query
                .OrderBy(n => n.CreatedAt)
                .ThenBy(n => n.Id),
            _ => query
                .OrderByDescending(n => n.CreatedAt)
                .ThenByDescending(n => n.Id)
        };
    }
    public async Task<OneOf<New, NewsServiceResult>> Edit(
        long key, NewsEditDTO edit, CancellationToken ct = default)
    {
        IEditSingle<long, NewsEditDTO, New, NewsServiceResult> editor = this;

        return await editor.EditSingle(key, edit, NewsServiceResult.Success,
            NewsServiceResult.NewNotFound, NewsServiceResult.DBError, ct);
    }
    public Task<NewsServiceResult> ValidateEdit(
        NewsEditDTO edit, CancellationToken ct = default)
    {
        if (!_accountService.IsModeratorOrAdmin())
            return Task.FromResult(NewsServiceResult.Unauthorized);

        if (edit.Title != null &&
            (string.IsNullOrWhiteSpace(edit.Title) ||
            edit.Title.Trim().Length > MAX_TITLE_LENGTH))
            return Task.FromResult(NewsServiceResult.InvalidTitle);

        return Task.FromResult(edit.Text != null &&
            string.IsNullOrWhiteSpace(edit.Text) ?
            NewsServiceResult.InvalidText : NewsServiceResult.Success);
    }
    public async Task<OneOf<New?, NewsServiceResult>> FindEdit(
        long key, CancellationToken ct = default)
    {
        var found = await _dbHelperService.TryFunction(
            cancel => _context.News
                .FirstOrDefaultAsync(news => news.Id == key, cancel),
            NewsServiceResult.DBError,
            ct: ct);

        return found.TryPickT0(out var news, out var error) ?
            news : error;
    }
    public async Task<NewsServiceResult> EditInternal(
        New item, NewsEditDTO edit, CancellationToken ct = default)
    {
        item.Title = edit.Title?.Trim() ?? item.Title;
        item.Text = edit.Text?.Trim() ?? item.Text;

        return await _dbHelperService.TrySyncContext(ct) ?
            NewsServiceResult.Success : NewsServiceResult.DBError;
    }
    public Task<OneOf<New, NewsServiceResult>> Create(
        NewsCreateDTO create, CancellationToken ct = default)
    {
        ICreate<NewsCreateDTO, New, NewsServiceResult> creator = this;

        return creator.CreateItem(create, NewsServiceResult.Success, ct);
    }
    public Task<NewsServiceResult> ValidateCreate(
        NewsCreateDTO create, CancellationToken ct = default)
    {
        if (!_accountService.IsModeratorOrAdmin())
            return Task.FromResult(NewsServiceResult.Unauthorized);

        if (string.IsNullOrWhiteSpace(create.Title) ||
            create.Title.Trim().Length > MAX_TITLE_LENGTH)
            return Task.FromResult(NewsServiceResult.InvalidTitle);

        return Task.FromResult(string.IsNullOrWhiteSpace(create.Text) ?
            NewsServiceResult.InvalidText : NewsServiceResult.Success);
    }
    public async Task<OneOf<New, NewsServiceResult>> CreateInternal(
        NewsCreateDTO create, CancellationToken ct = default)
    {
        var userId = _accountService.GetUserID();
        if (string.IsNullOrWhiteSpace(userId))
            return NewsServiceResult.Unauthorized;

        var news = new New
        {
            Id = 0,
            PosterUserId = userId,
            CreatedAt = DateTime.UtcNow,
            Title = create.Title.Trim(),
            Text = create.Text.Trim()
        };

        _context.News.Add(news);

        return await _dbHelperService.TrySyncContext(news,
            NewsServiceResult.DBError, ct);
    }
}
