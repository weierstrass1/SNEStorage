using OneOf;
using SNEStorage.DTOs;
using SNEStorage.DTOs.QueryOperations;
using SNEStorage.Models;

namespace SNEStorage.Services;

public partial class NewsService
{
    public Task<ServiceResultContext> ValidateEdit(NewsEditDTO edit, CancellationToken ct = default)
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

    public Task<OneOf<New, ServiceResultContext>> FindEdit(long key, CancellationToken ct = default)
    {
        var parameter = new GetByPropertyQuery<New, long>(
            news => news.Id, key, ServiceResult.NewNotFound);

        return _getById.Execute(_context.News, parameter, ct: ct);
    }

    public async Task<ServiceResultContext> EditInternal(New item, NewsEditDTO edit, 
        CancellationToken ct = default)
    {
        item.Title = edit.Title?.Trim() ?? item.Title;
        item.Text = edit.Text?.Trim() ?? item.Text;

        return await _dbHelperService.TrySyncContext(ct: ct);
    }
}
