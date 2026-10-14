using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Services;

public class ResourceAccessService(SnestorageContext context, AccountService accountService,
    DBHelperService dbHelperService, ILogger<ResourceAccessService> logger)
{
    private readonly SnestorageContext _context = context;
    private readonly AccountService _accountService = accountService;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly ILogger<ResourceAccessService> _logger = logger;

    public async Task<OneOf<ResourceAccessRow, ServiceResultContext>> GetAccess(
        long resourceId, CancellationToken ct = default)
    {
        string? userId = _accountService.GetUserID();

        var result = await _dbHelperService.TryFunction(
            cancel => _context.Resources.AsNoTracking()
                .Where(resource => resource.Id == resourceId)
                .Select(resource => new ResourceAccessRow(resource.Id, resource.ScoreId,
                    resource.VisibilityId, userId != null &&
                    (resource.SubmitterId == userId ||
                    resource.ResourcesAuthors.Any(author => author.UserId == userId) ||
                    resource.ResourceVersions.Any(version => version.SubmitterUserId == userId))))
                .FirstOrDefaultAsync(cancel), ServiceResult.DBError, ct: ct);

        if (!result.TryPickT0(out var access, out var error))
            return error;

        return access is null ? ServiceResult.ResourceNotFound.Return(_logger) : access;
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
