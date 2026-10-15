using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Services;

public partial class FlagsService
{
    public IQueryable<Flag> GetFilter(IQueryable<Flag> query, FlagFilterDTO filter)
    {
        if (filter.CreatedAtFrom is { } from)
        {
            var limit = from.Date;
            query = query.Where(r => r.CreatedAt >= limit);
        }
        if (filter.CreatedAtTo is { } to)
        {
            var limit = to.Date.AddDays(1);
            query = query.Where(r => r.CreatedAt < limit);
        }
        if (filter.LowestPriority != null)
            query = query.Where(r => r.Reason!.Priority >= filter.LowestPriority);
        if (filter.HighestPriority != null)
            query = query.Where(r => r.Reason!.Priority <= filter.HighestPriority);
        if (filter.Resolved != null)
            query = query.Where(r => r.Resolved == filter.Resolved);
        if (filter.ReasonID != null)
            query = query.Where(r => r.ReasonId == filter.ReasonID);
        return query;
    }
    public IQueryable<FlagReadDTO> GetSelect(IQueryable<Flag> query)
    {
        return query.Select(f => new FlagReadDTO
        {
            Id = f.Id,
            CreatedAt = f.CreatedAt,
            ReasonId = f.ReasonId,
            ReasonDescription = f.Reason!.Description,
            Priority = f.Reason.Priority,
            Resolved = f.Resolved,
            ResolvedAt = f.ResolvedAt,
            UserSummary = new UserNameDTO
            {
                Id = f.UserId,
                Username = f.User!.UserName!,
                UsernameColor = f.User.UserInfo!.UsernameColor,
                Avatar = new()
                {
                    Id = f.User.UserInfo.AvatarId,
                    FileTypeId = f.User.UserInfo.Avatar!.FileTypeId,
                    URL = f.User.UserInfo.Avatar.URL
                }
            }
        });
    }
    public IQueryable<Flag> GetSort(IQueryable<Flag> query, FlagSort sort)
    {
        return sort switch
        {
            FlagSort.HighestPriority => query.OrderByDescending(f => f.Reason!.Priority)
                                            .ThenByDescending(f => f.CreatedAt)
                                            .ThenByDescending(f => f.Id),
            FlagSort.LowestPriority => query.OrderBy(f => f.Reason!.Priority)
                                            .ThenByDescending(f => f.CreatedAt)
                                            .ThenByDescending(f => f.Id),
            FlagSort.RecentLast => query.OrderBy(f => f.CreatedAt)
                                            .ThenBy(f => f.Id),
            _ => query.OrderByDescending(f => f.CreatedAt)
                                            .ThenByDescending(f => f.Id),
        };
    }
}