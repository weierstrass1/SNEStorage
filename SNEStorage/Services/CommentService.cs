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

public class CommentService(SnestorageContext context, DBHelperService dbHelperService,
    AccountService accountService, ILogger<CommentService> logger)
{
    public const int MAX_AMOUNT_OF_COMMENTS_PER_PAGE = 200;

    private readonly SnestorageContext _context = context;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly AccountService _accountService = accountService;
    private readonly ILogger<CommentService> _logger = logger;

    private static IOrderedQueryable<Comment> SortComments(
        IQueryable<Comment> query, CommentSorting sort)
    {
        var ordered = sort switch
        {
            CommentSorting.RecentFirst =>
                query.OrderByDescending(comment => comment.PublishDate),

            CommentSorting.RecentLast =>
                query.OrderBy(comment => comment.PublishDate),

            CommentSorting.MostVoted =>
                query.OrderByDescending(comment => comment.Score!.LikesUsers.Count())
                    .ThenByDescending(comment => comment.PublishDate),

            CommentSorting.LessVoted =>
                query.OrderBy(comment => comment.Score!.LikesUsers.Count())
                    .ThenByDescending(comment => comment.PublishDate),

            CommentSorting.MostLiked =>
                query.OrderByDescending(comment =>
                        comment.Score!.LikesUsers.Count(vote => vote.Value))
                    .ThenByDescending(comment => comment.Score!.LikesUsers.Count())
                    .ThenByDescending(comment => comment.PublishDate),

            CommentSorting.MostDisliked =>
                query.OrderByDescending(comment =>
                        comment.Score!.LikesUsers.Count(vote => !vote.Value))
                    .ThenByDescending(comment => comment.Score!.LikesUsers.Count())
                    .ThenByDescending(comment => comment.PublishDate),

            CommentSorting.BestScore =>
                query.OrderByDescending(comment =>
                        comment.Score!.LikesUsers.Average(
                            vote => (double?)(vote.Value ? 1 : 0)) ?? 0.0)
                    .ThenByDescending(comment => comment.Score!.LikesUsers.Count())
                    .ThenByDescending(comment => comment.PublishDate),

            CommentSorting.WorstScore =>
                query.OrderBy(comment =>
                        comment.Score!.LikesUsers.Average(
                            vote => (double?)(vote.Value ? 1 : 0)) ?? 0.0)
                    .ThenByDescending(comment => comment.Score!.LikesUsers.Count())
                    .ThenByDescending(comment => comment.PublishDate),

            _ => throw new ArgumentOutOfRangeException(
                nameof(sort), sort, "Unsupported comment sorting.")
        };

        return ordered.ThenByDescending(comment => comment.Id);
    }

    public async Task<OneOf<LikeUsers, ServiceResultContext>> AddEditOrDeleteLike(
        long commentId, LikeValues value, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(value))
            return ServiceResult.UndefinedLike.Return(_logger);

        var permissionResult = await CheckResourcePermissionFromComment(commentId, ct);

        if (!permissionResult.TryPickT0(out var comment, out var permissionError))
            return permissionError;

        string? userId = _accountService.GetUserID();

        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult.Unauthorized.Return(_logger);

        if (comment.UserId == userId)
            return ServiceResult.CantLikeYourOwnComment.Return(_logger);

        if (comment.Deleted)
            return ServiceResult.CantLikeDeletedComment.Return(_logger);

        var likeQuery = _context.LikesUsers
            .Include(like => like.Score)
            .ThenInclude(score => score!.Comment)
            .Where(like => like.UserId == userId &&
                like.Score != null &&
                like.Score.Comment != null &&
                like.Score.Comment.Id == commentId);

        var likeResult = await _dbHelperService.TryFunction(
            cancel => likeQuery.FirstOrDefaultAsync(cancel),
            ServiceResult.DBError, ct: ct);

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
                ScoreId = comment.ScoreId
            };

            _context.LikesUsers.Add(like);
        }

        like.Value = value == LikeValues.Like;
        return await _dbHelperService.TrySyncContext(like, ct: ct);
    }

    public async Task<OneOf<List<CommentReadDto>, ServiceResultContext>> GetReplies(
        long commentId, int maxAmount = 20, int page = 0,
        CommentSorting sort = CommentSorting.RecentFirst,
        CancellationToken ct = default)
    {
        var startResult = GetStart(maxAmount, page, sort);

        if (!startResult.TryPickT0(out var query, out var startError))
            return startError;

        var permission = await CheckResourcePermissionFromCommentId(commentId, ct);

        if (permission.Result != ServiceResult.Success)
            return permission;

        query = query.Where(comment =>
            comment.Parent != null &&
            comment.Parent.ParentCommentId == commentId);

        return await GetEnd(query, maxAmount, page, sort, ct);
    }

    public async Task<OneOf<List<CommentReadDto>, ServiceResultContext>>
        GetResourceComments(long resourceId, int maxAmount = 20, int page = 0,
            CommentSorting sort = CommentSorting.RecentFirst,
            CancellationToken ct = default)
    {
        var startResult = GetStart(maxAmount, page, sort);

        if (!startResult.TryPickT0(out var query, out var startError))
            return startError;

        var permission = await CheckResourcePermission(resourceId, ct);

        if (permission.Result != ServiceResult.Success)
            return permission;

        query = query.Where(comment =>
            comment.ResourceComment != null &&
            comment.ResourceComment.ResourceId == resourceId &&
            comment.Parent == null);

        return await GetEnd(query, maxAmount, page, sort, ct);
    }

    public async Task<OneOf<LikeUsers, ServiceResultContext>> GetUserLike(
        long commentId, CancellationToken ct = default)
    {
        string? userId = _accountService.GetUserID();

        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult.Unauthorized.Return(_logger);

        var likeQuery = _context.LikesUsers.AsNoTracking()
            .Where(like => like.UserId == userId &&
                like.Score!.Comment != null &&
                like.Score.Comment.Id == commentId);

        var likeResult = await _dbHelperService.TryFunction(
            cancel => likeQuery.FirstOrDefaultAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (!likeResult.TryPickT0(out var like, out var likeError))
            return likeError;

        return like is not null ?
            like : ServiceResult.LikeNotFound.Return(_logger);
    }

    public async Task<OneOf<CommentComment, ServiceResultContext>> AddCommentToComment(
        long commentId, string commentText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(commentText))
            return ServiceResult.EmptyComment.Return(_logger);

        var commentQuery = _context.Comments
            .Include(comment => comment.ResourceComment)
            .ThenInclude(resourceComment => resourceComment!.Resource)
            .Where(comment => comment.Id == commentId);

        var commentResult = await _dbHelperService.TryFunction(
            cancel => commentQuery.FirstOrDefaultAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (!commentResult.TryPickT0(out var comment, out var commentError))
            return commentError;

        if (comment is null)
            return ServiceResult.CommentNotFound.Return(_logger);

        string? userId = _accountService.GetUserID();

        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult.Unauthorized.Return(_logger);

        if (comment.Deleted)
            return ServiceResult.CantReplyDeletedComment.Return(_logger);

        Resource? resource = null;

        if (comment.ResourceComment is not null)
        {
            resource = comment.ResourceComment.Resource;

            var permission = await CheckResourcePermission(
                comment.ResourceComment.ResourceId, ct);

            if (permission.Result != ServiceResult.Success)
                return permission;
        }

        var userResult = await UserNameDTO.FromUser(_accountService);

        if (!userResult.TryPickT0(out var user, out var userError))
        {
            return userError.Result == ServiceResult.UserNotFound ?
                ServiceResult.Unauthorized.Return(_logger) : userError;
        }

        var commentRelation = new CommentComment
        {
            Id = 0,
            ParentCommentId = commentId,
            ParentComment = comment,
            ChildCommentId = 0,
            ChildComment = new Comment
            {
                Id = 0,
                Score = new Score(),
                UserId = userId,
                Text = commentText,
                PublishDate = DateTime.UtcNow,
                UserSummary = user
            }
        };

        if (resource is not null)
        {
            commentRelation.ChildComment.ResourceComment = new ResourceComments
            {
                Id = 0,
                CommentId = 0,
                ResourceId = 0,
                Resource = resource
            };
        }

        comment.Childrens.Add(commentRelation);

        return await _dbHelperService.TrySyncContext(
            commentRelation, ct: ct);
    }

    public async Task<ServiceResultContext> EditComment(
        long commentId, string commentText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(commentText))
            return ServiceResult.EmptyComment.Return(_logger);

        var commentQuery = _context.Comments
            .Where(comment => comment.Id == commentId);

        var commentResult = await _dbHelperService.TryFunction(
            cancel => commentQuery.FirstOrDefaultAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (!commentResult.TryPickT0(out var comment, out var commentError))
            return commentError;

        if (comment is null)
            return ServiceResult.CommentNotFound.Return(_logger);

        string? userId = _accountService.GetUserID();
        bool isModeratorOrAdmin = _accountService.IsModeratorOrAdmin();

        if (comment.UserId != userId && !isModeratorOrAdmin)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return ServiceResult.Unauthorized.Return(_logger);

            return ServiceResult.Forbidden.Return(_logger);
        }

        comment.Text = commentText;
        comment.UpdatedAt = DateTime.UtcNow;

        return await _dbHelperService.TrySyncContext(ct: ct);
    }

    public async Task<ServiceResultContext> DeleteComment(
        long commentId, CancellationToken ct = default)
    {
        var commentQuery = _context.Comments
            .Include(comment => comment.Childrens)
            .Include(comment => comment.Parent)
            .Include(comment => comment.ResourceComment)
            .Where(comment => comment.Id == commentId);

        var commentResult = await _dbHelperService.TryFunction(
            cancel => commentQuery.FirstOrDefaultAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (!commentResult.TryPickT0(out var comment, out var commentError))
            return commentError;

        if (comment is null)
            return ServiceResult.CommentNotFound.Return(_logger);

        string? userId = _accountService.GetUserID();
        bool isModeratorOrAdmin = _accountService.IsModeratorOrAdmin();

        if (comment.UserId != userId && !isModeratorOrAdmin)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return ServiceResult.Unauthorized.Return(_logger);

            return ServiceResult.Forbidden.Return(_logger);
        }

        if (comment.Childrens.Count == 0)
        {
            _context.Comments.Remove(comment);
        }
        else
        {
            comment.Deleted = true;
            comment.Text = "";
            comment.UpdatedAt = DateTime.UtcNow;
        }

        return await _dbHelperService.TrySyncContext(ct: ct);
    }

    private async Task<OneOf<Comment, ServiceResultContext>>
        CheckResourcePermissionFromComment(
            long commentId, CancellationToken ct)
    {
        var commentQuery = _context.Comments.AsNoTracking()
            .Include(comment => comment.ResourceComment)
            .Where(comment =>
                comment.Id == commentId &&
                comment.ResourceComment != null);

        var commentResult = await _dbHelperService.TryFunction(
            cancel => commentQuery.FirstOrDefaultAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (!commentResult.TryPickT0(out var comment, out var commentError))
            return commentError;

        if (comment is null)
            return ServiceResult.CommentNotFound.Return(_logger);

        var permission = await CheckResourcePermission(
            comment.ResourceComment!.ResourceId, ct);

        return permission.Result == ServiceResult.Success ?
            comment : permission;
    }

    private async Task<ServiceResultContext> CheckResourcePermissionFromCommentId(
        long commentId, CancellationToken ct)
    {
        var permission = await CheckResourcePermissionFromComment(commentId, ct);

        return permission.TryPickT0(out _, out var permissionError) ?
            ServiceResult.Success.Return(_logger) : permissionError;
    }

    private async Task<ServiceResultContext> CheckResourcePermission(
        long resourceId, CancellationToken ct)
    {
        var resourceQuery = _context.Resources.AsNoTracking()
            .Include(resource => resource.ResourceVersions)
            .Include(resource => resource.ResourcesAuthors)
            .Where(resource => resource.Id == resourceId);

        var resourceResult = await _dbHelperService.TryFunction(
            cancel => resourceQuery.FirstOrDefaultAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (!resourceResult.TryPickT0(out var resource, out var resourceError))
            return resourceError;

        if (resource is null)
            return ServiceResult.ResourceNotFound.Return(_logger);

        string? userId = _accountService.GetUserID();

        bool hasPermission = resource.VisibilityId != 3 ||
            resource.SubmitterId == userId ||
            resource.ResourceVersions.Any(version =>
                version.SubmitterUserId == userId) ||
            resource.ResourcesAuthors.Any(author => author.UserId == userId) ||
            _accountService.IsModeratorOrAdmin();

        if (hasPermission)
            return ServiceResult.Success.Return(_logger);

        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult.Unauthorized.Return(_logger);

        return ServiceResult.Forbidden.Return(_logger);
    }

    private OneOf<IQueryable<Comment>, ServiceResultContext> GetStart(
        int maxAmount, int page, CommentSorting sort)
    {
        if (maxAmount > MAX_AMOUNT_OF_COMMENTS_PER_PAGE)
            return ServiceResult.AmountOfCommentsSurpassTheLimit.Return(_logger);

        if (!Enum.IsDefined(sort))
            return ServiceResult.UndefinedSort.Return(_logger);

        long skip = (long)maxAmount * page;

        if (skip > int.MaxValue)
            return ServiceResult.SkipOutOfRange.Return(_logger);

        if (page < 0)
            return ServiceResult.NegativePage.Return(_logger);

        if (maxAmount < 0)
            return ServiceResult.InvalidAmountOfCommentsPerPage.Return(_logger);

        return OneOf<IQueryable<Comment>, ServiceResultContext>
            .FromT0(_context.Comments.AsNoTracking());
    }

    private async Task<OneOf<List<CommentReadDto>, ServiceResultContext>> GetEnd(
        IQueryable<Comment> query, int maxAmount, int page,
        CommentSorting sort, CancellationToken ct = default)
    {
        string? userId = _accountService.GetUserID();
        int skip = (int)((long)maxAmount * page);

        query = query.Where(comment =>
            !comment.Deleted || comment.Childrens.Any());

        var resultQuery = SortComments(query, sort)
            .Skip(skip)
            .Take(maxAmount)
            .Select(comment => new CommentReadDto
            {
                Id = comment.Id,
                UserId = comment.UserId,
                ScoreId = comment.ScoreId,
                Text = comment.Text,
                Deleted = comment.Deleted,
                PublishDate = comment.PublishDate,
                UpdatedAt = comment.UpdatedAt,

                Likes = comment.Score!.LikesUsers.Count(vote => vote.Value),
                Dislikes = comment.Score!.LikesUsers.Count(vote => !vote.Value),
                HasReplies = comment.Childrens.Any(),

                MyVote = comment.Score!.LikesUsers
                    .Where(vote => userId != null && vote.UserId == userId)
                    .OrderBy(vote => vote.Id)
                    .Select(vote => (LikeValues?)(vote.Value ?
                        LikeValues.Like : LikeValues.Dislike))
                    .FirstOrDefault() ?? LikeValues.Remove,

                UserSummary = comment.User == null ?
                    null :
                    new UserNameDTO
                    {
                        Id = comment.UserId,
                        Username = comment.User.UserName!,
                        UsernameColor = comment.User.UserInfo == null ?
                            null : comment.User.UserInfo.UsernameColor,

                        Avatar = comment.User.UserInfo != null &&
                            comment.User.UserInfo.Avatar != null ?
                                new FileResponse
                                {
                                    Id = comment.User.UserInfo.Avatar.Id,
                                    URL = comment.User.UserInfo.Avatar.URL,
                                    FileTypeId =
                                        comment.User.UserInfo.Avatar.FileTypeId
                                } :
                                null
                    }
            });

        return await _dbHelperService.TryFunction(
            cancel => resultQuery.ToListAsync(cancel),
            ServiceResult.DBError, ct: ct);
    }
}
