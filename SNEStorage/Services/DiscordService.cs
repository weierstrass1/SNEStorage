using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneOf;
using SNEStorage.Discord;
using SNEStorage.Models;
using System.Globalization;
using System.Net;

namespace SNEStorage.Services;

public enum DiscordBotNotificationResult
{
    Success,
    Disabled,
    InvalidEvent,
    InvalidConfiguration,
    Unauthorized,
    EntityNotFound,
    Ineligible,
    UnsupportedEvent,
    Unavailable
}
public enum DiscordServiceResult
{
    Success,
    InvalidUserId,
    InvalidDiscordUserId,
    InvalidDiscordUsername,
    UserNotFound,
    DiscordAccountNotLinked,
    EmailNotConfirmed,
    BannedUser,
    DiscordAccountLinkedToAnotherUser,
    DBError,
    Unauthorized,
    InvalidOAuthCode,
    OAuthTokenExchangeError,
    OAuthUserLookupError,
}
public class DiscordService(
    SnestorageContext context,
    DBHelperService dbHelperService,
    IHttpClientFactory httpClientFactory,
    IOptions<DiscordDTO> discordOptions)
{
    private readonly SnestorageContext _context = context;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly DiscordDTO _discordOptions = discordOptions.Value;
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
            return DiscordServiceResult.DiscordAccountNotLinked;
        if (!member.EmailConfirmed)
            return DiscordServiceResult.EmailNotConfirmed;
        if (member.IsBanned)
            return DiscordServiceResult.BannedUser;

        return new DiscordMemberStateDTO(member.UserId, member.Nickname, 
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
            return DiscordServiceResult.DiscordAccountLinkedToAnotherUser;

        info.DiscordUserId = discordUserId;
        info.DiscordUsername = discordUsername;
        info.DiscordLinkedAt = DateTime.UtcNow;

        return await _dbHelperService.TrySyncContext(ct) ?
            DiscordServiceResult.Success :
            DiscordServiceResult.DBError;
    }
    public string CreateAuthorizationUrl(string state)
    {
        string redirectUri = GetOAuthRedirectUri();

        var parameters = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = _discordOptions.ApplicationId,
            ["scope"] = "identify",
            ["redirect_uri"] = redirectUri,
            ["state"] = state
        };

        return QueryHelpers.AddQueryString("https://discord.com/oauth2/authorize",
            parameters);
    }
    public async Task<DiscordServiceResult> CompleteAuthorizationAsync(string? sneStorageUserId,
        string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sneStorageUserId))
            return DiscordServiceResult.InvalidUserId;

        if (string.IsNullOrWhiteSpace(code))
            return DiscordServiceResult.InvalidOAuthCode;

        HttpClient client = _httpClientFactory.CreateClient("Discord");

        var tokenForm = new Dictionary<string, string>
        {
            ["client_id"] = _discordOptions.ApplicationId,
            ["client_secret"] = _discordOptions.ClientSecret,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = GetOAuthRedirectUri()
        };

        using var tokenResponse = await _dbHelperService.TryFunction(
            async cancel => await client.PostAsync("api/oauth2/token",
                new FormUrlEncodedContent(tokenForm), cancel), ct: ct);

        if (tokenResponse is null || !tokenResponse.IsSuccessStatusCode)
            return DiscordServiceResult.OAuthTokenExchangeError;

        var token = await _dbHelperService.TryFunction(
            tokenResponse.Content.ReadFromJsonAsync<DiscordOAuthTokenDTO>, ct: ct);

        if (token is null || string.IsNullOrWhiteSpace(token?.AccessToken))
            return DiscordServiceResult.OAuthTokenExchangeError;

        using HttpRequestMessage userRequest = new(HttpMethod.Get, "api/v10/users/@me");

        userRequest.Headers.Authorization = new("Bearer", token.AccessToken);

        using var userResponse = await _dbHelperService.TryFunction(
            async cancel => await client.SendAsync(
            userRequest, cancel), ct: ct);

        if (userResponse is null || !userResponse.IsSuccessStatusCode)
            return DiscordServiceResult.OAuthUserLookupError;

        var discordUser = await _dbHelperService.TryFunction(
            userResponse.Content.ReadFromJsonAsync<DiscordOAuthUserDTO>, ct: ct);

        if (discordUser is null || string.IsNullOrWhiteSpace(discordUser.Id) ||
            string.IsNullOrWhiteSpace(discordUser.Username))
            return DiscordServiceResult.OAuthUserLookupError;

        var linkResult = await LinkAccountAsync(sneStorageUserId, discordUser.Id,
            discordUser.Username, ct);

        if (linkResult != DiscordServiceResult.Success)
            return linkResult;

        _ = await NotifyMemberChangedAsync(
            discordUser.Id,
            ct);

        return DiscordServiceResult.Success;
    }
    public async Task<DiscordBotNotificationResult> NotifyMemberChangedAsync(
        string? discordUserId, CancellationToken ct = default)
    {
        return await NotifyBotAsync(new (Guid.NewGuid(), DiscordBotEventTypes.MemberChanged,
                discordUserId ?? string.Empty, DateTimeOffset.UtcNow), ct);
    }
    public async Task<DiscordBotNotificationResult> NotifyBotAsync(DiscordBotEventDTO notification,
        CancellationToken ct = default)
    {
        if (!_discordOptions.Enabled)
            return DiscordBotNotificationResult.Disabled;

        if (notification.EventId == Guid.Empty ||
            string.IsNullOrWhiteSpace(notification.Type) ||
            string.IsNullOrWhiteSpace(notification.EntityId) ||
            notification.OccurredAtUtc == default)
            return DiscordBotNotificationResult.InvalidEvent;

        if (string.IsNullOrWhiteSpace(_discordOptions.BotApiKey) ||
            !Uri.TryCreate(
                _discordOptions.BotBaseUrl.TrimEnd('/') + "/",
                UriKind.Absolute,
                out var botBaseUri))
            return DiscordBotNotificationResult.InvalidConfiguration;

        var endpoint = new Uri(
            botBaseUri,
            "api/internal/discord/events");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(notification)
        };

        request.Headers.TryAddWithoutValidation("X-SNEStorage-Bot-Key", 
            _discordOptions.BotApiKey);

        HttpClient client = _httpClientFactory.CreateClient("DiscordBot");

        using var response = await _dbHelperService.TryFunction(
            async cancel => await client.SendAsync(request, cancel),
            ct: ct);

        if (response is null)
            return DiscordBotNotificationResult.Unavailable;

        if (response.IsSuccessStatusCode)
            return DiscordBotNotificationResult.Success;

        return response.StatusCode switch
        {
            HttpStatusCode.BadRequest =>
                DiscordBotNotificationResult.InvalidEvent,

            HttpStatusCode.Unauthorized =>
                DiscordBotNotificationResult.Unauthorized,

            HttpStatusCode.NotFound =>
                DiscordBotNotificationResult.EntityNotFound,

            HttpStatusCode.Forbidden =>
                DiscordBotNotificationResult.Ineligible,

            HttpStatusCode.NotImplemented =>
                DiscordBotNotificationResult.UnsupportedEvent,

            _ => DiscordBotNotificationResult.Unavailable
        };
    }
    private string GetOAuthRedirectUri()
    {
        return $"{_discordOptions.PublicBaseUrl.TrimEnd('/')}/" +
            $"{_discordOptions.OAuthCallbackPath.TrimStart('/')}";
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
