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
    public async Task<OneOf<DiscordMemberStateDTO, DiscordServiceResult>> GetMemberStateAsync(string discordUserId,
        CancellationToken ct)
    {
        if (!IsValidDiscordUserId(discordUserId))
            return DiscordServiceResult.InvalidDiscordUserId;

            var now = DateTime.UtcNow;

            var member = await _context.UserInfos
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
                .SingleOrDefaultAsync(ct);
        }

    public async Task<DiscordServiceResult> LinkAccountAsync(string sneStorageUserId, string discordUserId, string discordUsername,
        CancellationToken ct)
    {

    }
    private static DiscordMemberStateDTO Denied(string reason)
    {
        return new(false, reason, null, null, null, false, false);
    }
    private static bool IsValidDiscordUserId(string value)
    {
        return ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0;
    }
    private static string? NormalizeColor(string? color)
    {
        return color is not { Length: 7 } || color[0] != '#' ||
            !color[1..].All(Uri.IsHexDigit) ?
                null : color.ToUpperInvariant();
    }
}
