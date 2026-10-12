using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.Discord;
using SNEStorage.Models;
using System.Globalization;

namespace SNEStorage.Services;
public enum DiscordServiceResult
{
    Success,
    InvalidUserId,
    InvalidDiscordUserId,
    InvalidDiscordUsername,
    UserNotFound,
    EmailNotConfirmed,
    DiscordAccountAlreadyLinked,
    DBError
}
public class DiscordService(SnestorageContext context,
    DBHelperService dbHelperService)
{
    private readonly SnestorageContext _context = context;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    public async Task<OneOf<DiscordMemberStateDTO, DiscordServiceResult>>
        GetMemberStateAsync(string discordUserId, CancellationToken ct)
    {
        if (!IsValidDiscordUserId(discordUserId))
            return DiscordServiceResult.InvalidDiscordUserId;

        var now = DateTime.UtcNow;

        var memberRes = await _dbHelperService.TryFunction(
            async cancel => await _context.UserInfos
            .AsNoTracking()
            .Where(info => info.DiscordUserId == discordUserId)
            .Select(info => new
                {
                    info.UserId,
                    Nickname = info.User!.UserName,
                    info.UsernameColor,
                    info.User.EmailConfirmed,

                    IsBanned = info.User.Bans.Any(ban =>
                            ban.IsPermanent || ban.ExpireDate > now),

                    IsModerator = _context.UserClaims.Any(claim =>
                            claim.UserId == info.UserId &&
                            claim.ClaimType == "Moderator" &&
                            claim.ClaimValue == "1")
                })
            .SingleOrDefaultAsync(cancel), 
            DiscordServiceResult.DBError, ct: ct);
        if (!memberRes.TryPickT0(out var member, out var error))
            return error;
        if (member is null)
            return DiscordServiceResult.InvalidDiscordUserId;
        if (member.EmailConfirmed || member.IsBanned)
            return DiscordServiceResult.InvalidUserId;
        return new DiscordMemberStateDTO(true, "verified", member.UserId, member.Nickname,
            NormalizeColor(member.UsernameColor), member.IsModerator, false);
    }

    public async Task<DiscordServiceResult> LinkAccountAsync(string sneStorageUserId, string discordUserId, string discordUsername,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sneStorageUserId))
            return DiscordServiceResult.InvalidUserId;

        if (!IsValidDiscordUserId(discordUserId))
            return DiscordServiceResult.InvalidDiscordUserId;

        discordUsername = discordUsername.Trim();

        if (discordUsername.Length is < 2 or > 32)
            return DiscordServiceResult.InvalidDiscordUsername;

        var infoRes = await _dbHelperService.TryFunction(
            async cancel => await _context.UserInfos
            .Include(userInfo => userInfo.User)
            .SingleOrDefaultAsync(userInfo => userInfo.UserId == sneStorageUserId, cancel),
            DiscordServiceResult.DBError, ct: ct);
        if (!infoRes.TryPickT0(out var info, out var error))
            return error;

        if (info is null)
            return DiscordServiceResult.UserNotFound;

        if (info.User?.EmailConfirmed != true)
            return DiscordServiceResult.EmailNotConfirmed;

        var alreadyLinkedRes = await _dbHelperService.TryFunction(
            async cancel => await _context.UserInfos
            .AsNoTracking()
            .AnyAsync(
                userInfo =>
                    userInfo.DiscordUserId == discordUserId &&
                    userInfo.UserId != sneStorageUserId,
                cancel), DiscordServiceResult.DBError, ct: ct);

        if (!alreadyLinkedRes.TryPickT0(out var alreadyLinked, out error))
            return error;

        if (alreadyLinked)
            return DiscordServiceResult.DiscordAccountAlreadyLinked;

        info.DiscordUserId = discordUserId;
        info.DiscordUsername = discordUsername;
        info.DiscordLinkedAt = DateTime.UtcNow;

        return await _dbHelperService.TrySyncContext(ct) ?
            DiscordServiceResult.Success :
            DiscordServiceResult.DBError;
    }
    private static bool IsValidDiscordUserId(string value)
    {
        return ulong.TryParse(value, NumberStyles.None, 
            CultureInfo.InvariantCulture, out var id) && id > 0;
    }
    private static string? NormalizeColor(string? color)
    {
        return color is not { Length: 7 } || color[0] != '#' ||
            !color[1..].All(Uri.IsHexDigit) ?
                null : color.ToUpperInvariant();
    }
}
