using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;
using System.Globalization;
using System.Net;

namespace SNEStorage.Services;

public class DiscordService(
    SnestorageContext context,
    DBHelperService dbHelperService,
    IHttpClientFactory httpClientFactory,
    IOptions<DiscordDTO> discordOptions,
    ILogger<DiscordService> logger)
{
    private readonly SnestorageContext _context = context;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
    private readonly DiscordDTO _discordOptions = discordOptions.Value;
    private readonly ILogger<DiscordService> _logger = logger;

    public async Task<OneOf<DiscordMemberStateDTO, ServiceResultContext>> GetMemberStateAsync(
        string discordUserId, CancellationToken ct)
    {
        if (!IsValidDiscordUserId(discordUserId))
            return ServiceResult.InvalidDiscordUserId.Return(_logger);

        DateTime now = DateTime.UtcNow;

        var memberQuery = _context.UserInfos.AsNoTracking()
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
            });

        var memberResult = await _dbHelperService.TryFunction(
            cancel => memberQuery.SingleOrDefaultAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (!memberResult.TryPickT0(out var member, out var queryError))
            return queryError;

        if (member is null)
            return ServiceResult.DiscordAccountNotLinked.Return(_logger);

        if (!member.EmailConfirmed)
            return ServiceResult.EmailNotConfirmed.Return(_logger);

        if (member.IsBanned)
            return ServiceResult.BannedUser.Return(_logger);

        return new DiscordMemberStateDTO(member.UserId, member.Nickname,
            NormalizeColor(member.UsernameColor), member.IsModerator, false);
    }

    public async Task<ServiceResultContext> LinkAccountAsync(
        string sneStorageUserId, string discordUserId, string discordUsername,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sneStorageUserId))
            return ServiceResult.InvalidUserId.Return(_logger);

        if (!IsValidDiscordUserId(discordUserId))
            return ServiceResult.InvalidDiscordUserId.Return(_logger);

        if (string.IsNullOrWhiteSpace(discordUsername))
            return ServiceResult.InvalidDiscordUsername.Return(_logger);

        discordUsername = discordUsername.Trim();

        if (discordUsername.Length is < 2 or > 32)
            return ServiceResult.InvalidDiscordUsername.Return(_logger);

        var infoQuery = _context.UserInfos
            .Include(userInfo => userInfo.User)
            .Where(userInfo => userInfo.UserId == sneStorageUserId);

        var infoResult = await _dbHelperService.TryFunction(
            cancel => infoQuery.SingleOrDefaultAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (!infoResult.TryPickT0(out var info, out var infoError))
            return infoError;

        if (info is null)
            return ServiceResult.UserNotFound.Return(_logger);

        if (info.User?.EmailConfirmed != true)
            return ServiceResult.EmailNotConfirmed.Return(_logger);

        var linkedQuery = _context.UserInfos.AsNoTracking()
            .Where(userInfo =>
                userInfo.DiscordUserId == discordUserId &&
                userInfo.UserId != sneStorageUserId);

        var linkedResult = await _dbHelperService.TryFunction(
            cancel => linkedQuery.AnyAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (!linkedResult.TryPickT0(out bool alreadyLinked, out var linkedError))
            return linkedError;

        if (alreadyLinked)
            return ServiceResult.DiscordAccountLinkedToAnotherUser.Return(_logger);

        info.DiscordUserId = discordUserId;
        info.DiscordUsername = discordUsername;
        info.DiscordLinkedAt = DateTime.UtcNow;

        return await _dbHelperService.TrySyncContext(ct: ct);
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

        return QueryHelpers.AddQueryString(
            "https://discord.com/oauth2/authorize", parameters);
    }

    public async Task<ServiceResultContext> CompleteAuthorizationAsync(
        string? sneStorageUserId, string? code, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(sneStorageUserId))
            return ServiceResult.InvalidUserId.Return(_logger);

        if (string.IsNullOrWhiteSpace(code))
            return ServiceResult.InvalidOAuthCode.Return(_logger);

        HttpClient client = _httpClientFactory.CreateClient("Discord");

        var tokenForm = new Dictionary<string, string>
        {
            ["client_id"] = _discordOptions.ApplicationId,
            ["client_secret"] = _discordOptions.ClientSecret,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = GetOAuthRedirectUri()
        };

        using var tokenContent = new FormUrlEncodedContent(tokenForm);

        var tokenResponseResult = await _dbHelperService.TryFunction(
            cancel => client.PostAsync("api/oauth2/token", tokenContent, cancel),
            ServiceResult.OAuthTokenExchangeError, ct: ct);

        if (!tokenResponseResult.TryPickT0(
            out var tokenResponse, out var tokenResponseError))
            return tokenResponseError;

        using var tokenResponseScope = tokenResponse;

        if (!tokenResponse.IsSuccessStatusCode)
            return ServiceResult.OAuthTokenExchangeError.Return(_logger);

        var tokenResult = await _dbHelperService.TryFunction(
            cancel => tokenResponse.Content.ReadFromJsonAsync<DiscordOAuthTokenDTO>(
                cancellationToken: cancel),
            ServiceResult.OAuthTokenExchangeError, ct: ct);

        if (!tokenResult.TryPickT0(out var token, out var tokenError))
            return tokenError;

        if (string.IsNullOrWhiteSpace(token?.AccessToken))
            return ServiceResult.OAuthTokenExchangeError.Return(_logger);

        using var userRequest =
            new HttpRequestMessage(HttpMethod.Get, "api/v10/users/@me");

        userRequest.Headers.Authorization = new("Bearer", token.AccessToken);

        var userResponseResult = await _dbHelperService.TryFunction(
            cancel => client.SendAsync(userRequest, cancel),
            ServiceResult.OAuthUserLookupError, ct: ct);

        if (!userResponseResult.TryPickT0(
            out var userResponse, out var userResponseError))
            return userResponseError;

        using var userResponseScope = userResponse;

        if (!userResponse.IsSuccessStatusCode)
            return ServiceResult.OAuthUserLookupError.Return(_logger);

        var discordUserResult = await _dbHelperService.TryFunction(
            cancel => userResponse.Content.ReadFromJsonAsync<DiscordOAuthUserDTO>(
                cancellationToken: cancel),
            ServiceResult.OAuthUserLookupError, ct: ct);

        if (!discordUserResult.TryPickT0(
            out var discordUser, out var discordUserError))
            return discordUserError;

        if (discordUser is null ||
            string.IsNullOrWhiteSpace(discordUser.Id) ||
            string.IsNullOrWhiteSpace(discordUser.Username))
            return ServiceResult.OAuthUserLookupError.Return(_logger);

        var linkResult = await LinkAccountAsync(
            sneStorageUserId, discordUser.Id, discordUser.Username, ct);

        if (linkResult.Result != ServiceResult.Success)
            return linkResult;

        _ = await NotifyMemberChangedAsync(discordUser.Id, ct);

        return ServiceResult.Success.Return(_logger);
    }

    public Task<ServiceResultContext> NotifyMemberChangedAsync(
        string? discordUserId, CancellationToken ct = default)
    {
        var notification = new DiscordBotEventDTO(
            Guid.NewGuid(),
            DiscordBotEventTypes.MemberChanged,
            discordUserId ?? string.Empty,
            DateTimeOffset.UtcNow);

        return NotifyBotAsync(notification, ct);
    }

    public async Task<ServiceResultContext> NotifyBotAsync(
        DiscordBotEventDTO notification, CancellationToken ct = default)
    {
        if (!_discordOptions.Enabled)
            return ServiceResult.Disabled.Return(_logger);

        if (notification.EventId == Guid.Empty ||
            string.IsNullOrWhiteSpace(notification.Type) ||
            string.IsNullOrWhiteSpace(notification.EntityId) ||
            notification.OccurredAtUtc == default)
            return ServiceResult.InvalidEvent.Return(_logger);

        if (string.IsNullOrWhiteSpace(_discordOptions.BotApiKey) ||
            !Uri.TryCreate(
                _discordOptions.BotBaseUrl.TrimEnd('/') + "/",
                UriKind.Absolute,
                out var botBaseUri))
            return ServiceResult.InvalidConfiguration.Return(_logger);

        var endpoint = new Uri(
            botBaseUri,
            "api/internal/discord/events");

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(notification)
        };

        request.Headers.TryAddWithoutValidation("X-SNEStorage-Bot-Key", _discordOptions.BotApiKey);

        HttpClient client = _httpClientFactory.CreateClient("DiscordBot");

        var responseResult = await _dbHelperService.TryFunction(
            cancel => client.SendAsync(request, cancel),
            ServiceResult.Unavailable, ct: ct);

        if (!responseResult.TryPickT0(out var response, out var responseError))
            return responseError;

        using var responseScope = response;

        if (response.IsSuccessStatusCode)
            return ServiceResult.Success.Return(_logger);

        ServiceResult result = response.StatusCode switch
        {
            HttpStatusCode.BadRequest =>
                ServiceResult.InvalidEvent,

            HttpStatusCode.Unauthorized =>
                ServiceResult.Unauthorized,

            HttpStatusCode.NotFound =>
                ServiceResult.EntityNotFound,

            HttpStatusCode.Forbidden =>
                ServiceResult.Ineligible,

            HttpStatusCode.NotImplemented =>
                ServiceResult.UnsupportedEvent,

            _ =>
                ServiceResult.Unavailable
        };

        return result.Return(_logger);
    }

    private string GetOAuthRedirectUri()
    {
        return $"{_discordOptions.PublicBaseUrl.TrimEnd('/')}/" +
            $"{_discordOptions.OAuthCallbackPath.TrimStart('/')}";
    }

    private static bool IsValidDiscordUserId(string value)
    {
        return ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture,
            out ulong id) && id > 0;
    }

    private static string? NormalizeColor(string? color)
    {
        return color is not { Length: 7 } ||
            color[0] != '#' ||
            !color[1..].All(Uri.IsHexDigit) ?
                null : color.ToUpperInvariant();
    }
}
