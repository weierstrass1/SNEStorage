using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Services;

public partial class NewsService
{
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
