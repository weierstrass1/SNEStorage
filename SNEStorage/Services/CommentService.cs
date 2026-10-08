using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Services;
public enum LikeValues
{
    Like,
    Dislike,
    Remove
}
public enum CommentSorting
{
    RecentFirst,
    RecentLast,
    MostVoted,
    LessVoted,
    MostLiked,
    MostDisliked,
    BestScore,
    WorstScore
}
public enum CommentServiceResult
{
    Success,
    CantLikeDeletedComment,
    CantReplyDeletedComment,
    CantLikeYourOwnComment,
    LikeNotFound,
    UndefinedLike,
    UndefinedSort,
    AmountOfCommentsSurpassTheLimit,
    InvalidAmountOfCommentsPerPage,
    NegativePage,
    SkipOutOfRange,
    ResourceNotFound,
    Unauthorized,
    CommentNotFound,
    EmptyComment,
    DBError
}
public class CommentService(SnestorageContext context,
    DBHelperService dbHelperService,
    AccountService accountService)
{
    public const int MAX_AMOUNT_OF_COMMENTS_PER_PAGE = 200;
    private readonly SnestorageContext _context = context;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly AccountService _accountService = accountService;
    private static IOrderedQueryable<Comment> SortComments(IQueryable<Comment> query,
        CommentSorting sort)
    {
        var ordered = sort switch
        {
            CommentSorting.RecentFirst => query
                .OrderByDescending(c => c.PublishDate),

            CommentSorting.RecentLast => query
                .OrderBy(c => c.PublishDate),

            CommentSorting.MostVoted => query
                .OrderByDescending(c => c.Score!.LikesUsers.Count())
                .ThenByDescending(c => c.PublishDate),

            CommentSorting.LessVoted => query
                .OrderBy(c => c.Score!.LikesUsers.Count())
                .ThenByDescending(c => c.PublishDate),

            CommentSorting.MostLiked => query
                .OrderByDescending(c =>
                    c.Score!.LikesUsers.Count(v => v.Value))
                .ThenByDescending(c => c.Score!.LikesUsers.Count())
                .ThenByDescending(c => c.PublishDate),

            CommentSorting.MostDisliked => query
                .OrderByDescending(c =>
                    c.Score!.LikesUsers.Count(v => !v.Value))
                .ThenByDescending(c => c.Score!.LikesUsers.Count())
                .ThenByDescending(c => c.PublishDate),

            CommentSorting.BestScore => query
                .OrderByDescending(c =>
                    c.Score!.LikesUsers.Average(
                        v => (double?)(v.Value ? 1 : 0)) ?? 0.0)
                .ThenByDescending(c => c.Score!.LikesUsers.Count())
                .ThenByDescending(c => c.PublishDate),

            CommentSorting.WorstScore => query
                .OrderBy(c =>
                    c.Score!.LikesUsers.Average(
                        v => (double?)(v.Value ? 1 : 0)) ?? 0.0)
                .ThenByDescending(c => c.Score!.LikesUsers.Count())
                .ThenByDescending(c => c.PublishDate),

            _ => throw new ArgumentOutOfRangeException(
                nameof(sort), sort, "Unsupported comment sorting.")
        };

        return ordered.ThenByDescending(c => c.Id);
    }
    public async Task<OneOf<LikeUsers, CommentServiceResult>> AddEditOrDeleteLike(long commentId, LikeValues value, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(value))
            return CommentServiceResult.UndefinedLike;

        var permission = await CheckResourcePermissionFromComment(commentId, ct);
        if (!permission.TryPickT0(out var comment, out var error))
            return error;

        string? userId = _accountService.GetUserID();

        if (userId == null)
            return CommentServiceResult.Unauthorized;
        if (comment.UserId == userId)
            return CommentServiceResult.CantLikeYourOwnComment;
        if (comment.Deleted)
            return CommentServiceResult.CantLikeDeletedComment;

        var likeRes = await _dbHelperService.TryFunction(
            async cancel => await _context.LikesUsers
                .Include(l => l.Score)
                    .ThenInclude(l => l!.Comment)
                .FirstOrDefaultAsync(l => l.UserId == userId &&
                l.Score != null &&
                l.Score.Comment != null &&
                l.Score.Comment.Id == commentId, cancel),
                CommentServiceResult.DBError, ct: ct);
        if (!likeRes.TryPickT0(out var like, out error))
            return error;

        if (value == LikeValues.Remove)
        {
            if (like == null)
                return CommentServiceResult.LikeNotFound;
            _context.LikesUsers.Remove(like);
            return await _dbHelperService.TrySyncContext(like, CommentServiceResult.DBError, ct);
        }
        
        if(like == null)
        {
            like = new()
            {
                Id = 0,
                UserId = userId,
                ScoreId = comment.ScoreId
            };
            _context.LikesUsers.Add(like);
        }

        like.Value = value == LikeValues.Like;
        return await _dbHelperService.TrySyncContext(like, CommentServiceResult.DBError, ct);
    }
    public async Task<OneOf<List<Comment>,CommentServiceResult>> GetReplies(long commentId, int maxAmount = 20, int page = 0, CommentSorting sort = CommentSorting.RecentFirst, CancellationToken ct = default)
    {
        if (!GetStart(maxAmount, page, sort).TryPickT0(out var query, out var error))
            return error;

        CommentServiceResult permission = await CheckResourcePermissionFromCommentId(commentId, ct);

        if (permission != CommentServiceResult.Success)
            return permission;

        query = query
            .Where(r => r.Parent != null && r.Parent.ParentCommentId == commentId);

        return await GetEnd(query, maxAmount, page, sort, ct);
    }
    private async Task<OneOf<Comment, CommentServiceResult>> CheckResourcePermissionFromComment(long commentId, CancellationToken ct)
    {
        var commentRes = await _dbHelperService.TryFunction(
                    async cancel => await _context.Comments
                        .AsNoTracking()
                        .Include(c => c.ResourceComment)
                        .FirstOrDefaultAsync(c => c.Id == commentId && c.ResourceComment != null, cancel),
                        CommentServiceResult.DBError, ct: ct);
        if (!commentRes.TryPickT0(out var comment, out var error))
            return error;
        if (comment == null)
            return CommentServiceResult.CommentNotFound;

        var permission = await
            CheckResourcePermission(comment.ResourceComment!.ResourceId, ct);

        return permission == CommentServiceResult.Success ?
            comment :
            permission;
    }
    private async Task<CommentServiceResult> CheckResourcePermissionFromCommentId(long commentId, CancellationToken ct)
    {
        var permission = await CheckResourcePermissionFromComment(commentId, ct);
        return permission.TryPickT0(out _, out var error) ?
            CommentServiceResult.Success :
            error;
    }

    public async Task<OneOf<List<Comment>,CommentServiceResult>> GetResourceComments(long resourceId, int maxAmount = 20, int page = 0, CommentSorting sort = CommentSorting.RecentFirst, CancellationToken ct = default)
    {
        if (!GetStart(maxAmount, page, sort).TryPickT0(out var query, out var error))
            return error;

        CommentServiceResult permission = await CheckResourcePermission(resourceId, ct);
        if(permission != CommentServiceResult.Success)
            return permission;

        query = query
                    .Where(r => r.ResourceComment != null &&
                        r.ResourceComment.ResourceId == resourceId &&
                        r.Parent == null);

        return await GetEnd(query, maxAmount, page, sort, ct);
    }

    private async Task<CommentServiceResult> CheckResourcePermission(long resourceId, CancellationToken ct)
    {
        var resourceRes = await _dbHelperService.TryFunction(
            async cancel => await _context.Resources
                .AsNoTracking()
                .Include(r => r.ResourceVersions)
                .Include(r => r.ResourcesAuthors)
                .FirstOrDefaultAsync(r => r.Id == resourceId, cancel),
                CommentServiceResult.DBError, ct: ct);
        if (!resourceRes.TryPickT0(out var resource, out var error))
            return error;
        if (resource == null)
            return CommentServiceResult.ResourceNotFound;

        string? userId = _accountService.GetUserID();

        if (resource.VisibilityId == 3 &&
            resource.SubmitterId != userId &&
            !resource.ResourceVersions.Any(r => r.SubmitterUserId == userId) &&
            !resource.ResourcesAuthors.Any(r => r.UserId == userId) &&
            !_accountService.IsModeratorOrAdmin())
            return CommentServiceResult.Unauthorized;
        return CommentServiceResult.Success;
    }

    private OneOf<IQueryable<Comment>, CommentServiceResult> GetStart(int maxAmount, int page, CommentSorting sort)
    {
        if (maxAmount > MAX_AMOUNT_OF_COMMENTS_PER_PAGE)
            return CommentServiceResult.AmountOfCommentsSurpassTheLimit;
        if (!Enum.IsDefined(sort))
            return CommentServiceResult.UndefinedSort;
        long skip = (long)maxAmount * page;
        if (skip > int.MaxValue)
            return CommentServiceResult.SkipOutOfRange;
        if (page < 0)
            return CommentServiceResult.NegativePage;
        if (maxAmount < 0)
            return CommentServiceResult.InvalidAmountOfCommentsPerPage;

        IQueryable<Comment> query = _context.Comments
                    .AsNoTracking()
                    .Include(r => r.Score)
                        .ThenInclude(r => r!.LikesUsers)
                    .Include(r => r.Childrens)
                    .Include(r => r.User)
                        .ThenInclude(u => u!.UserInfo)
                        .ThenInclude(info => info!.Avatar);
        return OneOf<IQueryable<Comment>, CommentServiceResult>.FromT0(query);
    }
    private async Task<OneOf<List<Comment>, CommentServiceResult>> GetEnd(IQueryable<Comment> query, int maxAmount, int page, CommentSorting sort, CancellationToken ct = default)
    {
        var res = await _dbHelperService.TryFunction(
            async cancel => await SortComments(query, sort)
            .Skip(maxAmount * page)
            .Take(maxAmount)
            .ToListAsync(cancel), ct: ct);
        if (res == null)
            return CommentServiceResult.DBError;
        UserNameDTO.FillUserSummaries((target, summary) => target.UserSummary = summary,
            () => res.Select(r => (r, r.User!)));
        return res;
    }
    public async Task<OneOf<LikeUsers, CommentServiceResult>> GetUserLike(long commentId, CancellationToken ct = default)
    {
        string? userId = _accountService.GetUserID();
        if (userId == null)
            return CommentServiceResult.Unauthorized;

        var res = await _dbHelperService.TryFunction(
            async cancel => await _context.LikesUsers
                .AsNoTracking()
                .FirstOrDefaultAsync(l => l.UserId == userId &&
                    l.Score!.Comment != null &&
                    l.Score.Comment.Id == commentId, cancel), CommentServiceResult.DBError, ct: ct);
        if (!res.TryPickT0(out var like, out var error))
            return error;
        return like != null ?
            like :
            CommentServiceResult.LikeNotFound;
    }
    public async Task<OneOf<CommentComment, CommentServiceResult>> AddCommentToComment(long commentId, string commentText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(commentText))
            return CommentServiceResult.EmptyComment;
        var commentRes = await _dbHelperService.TryFunction(
        async cancel => await _context.Comments
                .Include(c => c.ResourceComment)
                        .ThenInclude(c => c!.Resource)
            .FirstOrDefaultAsync(c => c.Id == commentId, cancel),
        CommentServiceResult.DBError, ct: ct);

        if (!commentRes.TryPickT0(out var comment, out var error))
            return error;
        if (comment == null)
            return CommentServiceResult.CommentNotFound;

        string? userId = _accountService.GetUserID();
        if (userId == null)
            return CommentServiceResult.Unauthorized;
        if (comment.Deleted)
            return CommentServiceResult.CantReplyDeletedComment;

        Resource? resource = null;

        if (comment.ResourceComment != null)
        {
            resource = comment.ResourceComment.Resource;
            var permission = await 
                CheckResourcePermission(comment.ResourceComment.ResourceId, ct);

            if (permission != CommentServiceResult.Success)
                return permission;
        }

        var user = await UserNameDTO.FromUser(_accountService);

        if (user == null)
            return CommentServiceResult.Unauthorized;

        CommentComment cc = new()
        {
            Id = 0,
            ParentCommentId = commentId,
            ParentComment = comment,
            ChildCommentId = 0,
            ChildComment = new()
            {
                Id = 0,
                Score = new Score(),
                UserId = userId,
                Text = commentText,
                PublishDate = DateTime.UtcNow,
                UserSummary = user
            }
        };
        if(resource != null)
        {
            cc.ChildComment.ResourceComment = new()
            {
                Id = 0,
                CommentId = 0,
                ResourceId = 0,
                Resource = resource
            };
        }

        comment.Childrens.Add(cc);
        return await _dbHelperService.TrySyncContext(cc, CommentServiceResult.DBError, ct);
    }
    public async Task<CommentServiceResult> EditComment(long commentId, string commentText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(commentText))
            return CommentServiceResult.EmptyComment;
        var commentRes = await _dbHelperService.TryFunction(
                async cancel => await _context.Comments
                    .FirstOrDefaultAsync(c => c.Id == commentId, cancel),
                CommentServiceResult.DBError, ct: ct);
        if (!commentRes.TryPickT0(out var comment, out var error))
            return error;
        if (comment == null)
            return CommentServiceResult.CommentNotFound;

        string? userId = _accountService.GetUserID();
        bool isModorAdmin = _accountService.IsModeratorOrAdmin();

        if (comment.UserId != userId && !isModorAdmin)
            return CommentServiceResult.Unauthorized;

        comment.Text = commentText;
        comment.UpdatedAt = DateTime.UtcNow;

        return await _dbHelperService.TrySyncContext(ct) ?
            CommentServiceResult.Success :
            CommentServiceResult.DBError;
    }
    public async Task<CommentServiceResult> DeleteComment(long commentId, CancellationToken ct = default)
    {
        var commentRes = await _dbHelperService.TryFunction(
            async cancel => await _context.Comments
                .Include(c => c.Childrens)
                .Include(c => c.Parent)
                .Include(c => c.ResourceComment)
                .FirstOrDefaultAsync(c => c.Id == commentId, cancel),
            CommentServiceResult.DBError, ct: ct);
        if (!commentRes.TryPickT0(out var comment, out var error))
            return error;
        if (comment == null)
            return CommentServiceResult.CommentNotFound;

        string? userId = _accountService.GetUserID();
        bool isModorAdmin = _accountService.IsModeratorOrAdmin();

        if (comment.UserId != userId && !isModorAdmin)
            return CommentServiceResult.Unauthorized;

        if (comment!.Childrens.Count <= 0)
            _context.Comments.Remove(comment);
        else
        {
            comment.Deleted = true;
            comment.Text = "";
            comment.UpdatedAt = DateTime.UtcNow;
        }

        return await _dbHelperService.TrySyncContext(ct) ?
            CommentServiceResult.Success :
            CommentServiceResult.DBError;
    }
}
