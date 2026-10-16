using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services.QueryComponents;

namespace SNEStorage.Services;

public class ResourceCommunityService(SnestorageContext context,
    AccountService accountService, ResourceAccessService accessService,
    DBHelperService dbHelperService,
    GetFirstOrErrorQueryOperation<ApplicationUser> getUser,
    GetFirstOrDefaultQueryOperation<ScoreUsers> getVote,
    GetFirstOrErrorQueryOperation<RatingReadDto> getRating,
    ILogger<ResourceCommunityService> logger)
{
    private readonly SnestorageContext _context = context;
    private readonly AccountService _accountService = accountService;
    private readonly ResourceAccessService _accessService = accessService;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly GetFirstOrErrorQueryOperation<ApplicationUser> _getUser = getUser;
    private readonly GetFirstOrDefaultQueryOperation<ScoreUsers> _getVote = getVote;
    private readonly GetFirstOrErrorQueryOperation<RatingReadDto> _getRating = getRating;
    private readonly ILogger<ResourceCommunityService> _logger = logger;

    public async Task<OneOf<ResourceComments, ServiceResultContext>> AddComment(
        long resourceId, string commentText, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(commentText))
            return ServiceResult.EmptyComment.Return(_logger);

        string? userId = _accountService.GetUserID();
        if (userId is null)
            return ServiceResult.Forbidden.Return(_logger);

        var userQuery = _context.Users
            .Include(user => user.UserInfo)
                .ThenInclude(info => info!.Avatar)
            .Where(user => user.Id == userId);

        var userResult = await _getUser.Execute(
            userQuery, ServiceResult.Forbidden, ct: ct);

        if (!userResult.TryPickT0(out var user, out var userError))
            return userError;

        var accessResult = await _accessService.GetViewAccess(resourceId, ct);
        if (!accessResult.TryPickT0(out _, out var accessError))
            return accessError;

        ResourceComments resourceComment = new()
        {
            Id = 0,
            ResourceId = resourceId,
            Comment = new()
            {
                Id = 0,
                Score = new Score(),
                UserId = userId,
                User = user,
                Text = commentText,
                PublishDate = DateTime.UtcNow
            }
        };

        resourceComment.Comment.UserSummary = UserNameDTO.FromApplicationUser(user);
        _context.ResourcesComments.Add(resourceComment);

        return await _dbHelperService.TrySyncContext(resourceComment, ct: ct);
    }

    public async Task<OneOf<ScoreUsers, ServiceResultContext>> AddOrEditScore(
        long resourceId, long value, CancellationToken ct = default)
    {
        if (value is < 1 or > 5)
            return ServiceResult.InvalidScore.Return(_logger);

        string? userId = _accountService.GetUserID();
        if (userId is null)
            return ServiceResult.Forbidden.Return(_logger);

        var accessResult = await _accessService.GetAccess(resourceId, ct);
        if (!accessResult.TryPickT0(out var access, out var accessError))
            return accessError;

        if (!access.ContentAllowed)
            return ServiceResult.Forbidden.Return(_logger);

        if (access.VisibilityId == 3)
            return ServiceResult.CantAddScoreToPrivateResource.Return(_logger);

        if (access.IsContributor)
            return ServiceResult.CantScoreSelfResource.Return(_logger);

        var voteQuery = _context.ScoresUsers.AsTracking()
            .Where(score => score.ScoreId == access.ScoreId &&
                score.UserId == userId);
        var voteResult = await _getVote.Execute(voteQuery, ct: ct);

        if (!voteResult.TryPickT0(out var vote, out var voteError))
            return voteError;

        if (vote is null)
        {
            vote = new ScoreUsers
            {
                Id = 0,
                ScoreId = access.ScoreId,
                UserId = userId,
                Value = value
            };

            _context.ScoresUsers.Add(vote);
        }

        vote.Value = value;
        return await _dbHelperService.TrySyncContext(vote, ct: ct);
    }

    public async Task<OneOf<RatingReadDto, ServiceResultContext>> GetRating(
        long resourceId, CancellationToken ct = default)
    {
        var accessResult = await _accessService.GetViewAccess(resourceId, ct);
        if (!accessResult.TryPickT0(out _, out var accessError))
            return accessError;

        string? userId = _accountService.GetUserID();

        var query = _context.Resources.AsNoTracking()
            .Where(resource => resource.Id == resourceId)
            .Select(resource => new RatingReadDto(
                resource.Score!.ScoresUsers
                    .Average(score => (double?)score.Value) ?? 0.0,
                resource.Score.ScoresUsers.Count(),
                resource.Score.ScoresUsers
                    .Where(score => userId != null && score.UserId == userId)
                    .Select(score => (long?)score.Value)
                    .FirstOrDefault()));

        return await _getRating.Execute(
            query, ServiceResult.ResourceNotFound, ct: ct);
    }
}
