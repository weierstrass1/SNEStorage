using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services.QueryComponents;

namespace SNEStorage.Services;

public class ResourceAccessService(SnestorageContext context,
    AccountService accountService,
    GetFirstOrErrorQueryOperation<ResourceAccessRow> getFirst,
    ILogger<ResourceAccessService> logger)
{
    private readonly SnestorageContext _context = context;
    private readonly AccountService _accountService = accountService;
    private readonly GetFirstOrErrorQueryOperation<ResourceAccessRow> _getFirst = getFirst;
    private readonly ILogger<ResourceAccessService> _logger = logger;

    public Task<OneOf<ResourceAccessRow, ServiceResultContext>> GetAccess(
        long resourceId, CancellationToken ct = default)
    {
        string? userId = _accountService.GetUserID();

        var query = _context.Resources.AsNoTracking()
            .Where(resource => resource.Id == resourceId)
            .Select(resource => new ResourceAccessRow(resource.Id, resource.ScoreId,
                resource.VisibilityId, userId != null &&
                (resource.SubmitterId == userId ||
                resource.ResourcesAuthors.Any(author => author.UserId == userId) ||
                resource.ResourceVersions.Any(version =>
                    version.SubmitterUserId == userId))));

        return _getFirst.Execute(query, ServiceResult.ResourceNotFound, ct: ct);
    }

    public async Task<OneOf<ResourceAccessRow, ServiceResultContext>> GetViewAccess(
        long resourceId, CancellationToken ct = default)
    {
        var result = await GetAccess(resourceId, ct);

        if (!result.TryPickT0(out var access, out var error))
            return error;

        var accessResult = CheckViewAccess(access);
        return accessResult.Result == ServiceResult.Success ? access : accessResult;
    }

    public async Task<OneOf<ResourceAccessRow, ServiceResultContext>> GetEditAccess(
        long resourceId, CancellationToken ct = default)
    {
        var result = await GetAccess(resourceId, ct);

        if (!result.TryPickT0(out var access, out var error))
            return error;

        var accessResult = CheckEditAccess(access);
        return accessResult.Result == ServiceResult.Success ? access : accessResult;
    }

    private ServiceResultContext CheckViewAccess(ResourceAccessRow access)
    {
        if (access.VisibilityId != 3 || _accountService.IsModeratorOrAdmin())
            return ServiceResult.Success.Return(_logger);

        if (_accountService.GetUserID() is null)
            return ServiceResult.UserNotFoundUnauthorized.Return(_logger);

        return access.IsContributor ?
            ServiceResult.Success.Return(_logger) :
            ServiceResult.Forbidden.Return(_logger);
    }

    private ServiceResultContext CheckEditAccess(ResourceAccessRow access)
    {
        if (_accountService.GetUserID() is null)
            return ServiceResult.UserNotFoundBadRequest.Return(_logger);

        return access.IsContributor ?
            ServiceResult.Success.Return(_logger) :
            ServiceResult.Forbidden.Return(_logger);
    }
}
