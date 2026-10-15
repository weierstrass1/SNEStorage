using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Services;

public partial class NewsService
{
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
}
