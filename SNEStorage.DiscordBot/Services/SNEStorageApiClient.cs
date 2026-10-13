using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using SNEStorage.DiscordBot.DTOs;

namespace SNEStorage.DiscordBot.Services;

public sealed class SNEStorageApiClient(
    HttpClient httpClient,
    IOptions<SNEStorageApiDTO> options,
    ILogger<SNEStorageApiClient> logger)
{
    private const string BotKeyHeader = "X-SNEStorage-Bot-Key";
    private readonly SNEStorageApiDTO _options = options.Value;

    public async Task<SNEStorageNewsLookupResult> GetNewsAsync(
        long newsId,
        CancellationToken cancellationToken = default)
    {
        if (newsId <= 0 ||
            string.IsNullOrWhiteSpace(_options.BaseUrl) ||
            !Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            return new(SNEStorageNewsLookupStatus.ConfigurationError);
        }

        var endpoint = new Uri(baseUri, $"api/news/{newsId}");

        try
        {
            using var response = await httpClient.GetAsync(
                endpoint,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new(SNEStorageNewsLookupStatus.NotFound);

            if (!response.IsSuccessStatusCode)
                return new(SNEStorageNewsLookupStatus.Unavailable);

            var news = await response.Content
                .ReadFromJsonAsync<SNEStorageNewsDTO>(
                    cancellationToken: cancellationToken);

            if (news is null || news.Id != newsId ||
                string.IsNullOrWhiteSpace(news.Title))
            {
                return new(SNEStorageNewsLookupStatus.InvalidResponse);
            }

            return new(SNEStorageNewsLookupStatus.Success, news);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return new(SNEStorageNewsLookupStatus.Unavailable);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(
                exception,
                "SNEStorage news lookup failed for news {NewsId}.",
                newsId);

            return new(SNEStorageNewsLookupStatus.Unavailable);
        }
    }

    public async Task<SNEStorageUserLookupResult> GetUserProfileAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(_options.BaseUrl) ||
            !Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            return new(SNEStorageUserLookupStatus.ConfigurationError);
        }

        var endpoint = new Uri(
            baseUri,
            $"api/profile/{Uri.EscapeDataString(userId)}");

        try
        {
            using var response = await httpClient.GetAsync(
                endpoint,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode is HttpStatusCode.NotFound or
                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden)
            {
                return new(SNEStorageUserLookupStatus.NotFound);
            }

            if (!response.IsSuccessStatusCode)
                return new(SNEStorageUserLookupStatus.Unavailable);

            var user = await response.Content
                .ReadFromJsonAsync<SNEStorageUserProfileDTO>(
                    cancellationToken: cancellationToken);

            if (user is null ||
                !string.Equals(user.UserId, userId, StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(user.UserSummary?.Username))
            {
                return new(SNEStorageUserLookupStatus.InvalidResponse);
            }

            return new(SNEStorageUserLookupStatus.Success, user);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return new(SNEStorageUserLookupStatus.Unavailable);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(
                exception,
                "SNEStorage user lookup failed for user {SNEStorageUserId}.",
                userId);

            return new(SNEStorageUserLookupStatus.Unavailable);
        }
    }

    public async Task<SNEStorageResourceLookupResult> GetResourceAsync(
        long resourceId,
        CancellationToken cancellationToken = default)
    {
        if (resourceId <= 0 ||
            string.IsNullOrWhiteSpace(_options.BaseUrl) ||
            !Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            return new(SNEStorageResourceLookupStatus.ConfigurationError);
        }

        var endpoint = new Uri(baseUri, $"api/resource/{resourceId}");

        try
        {
            using var response = await httpClient.GetAsync(
                endpoint,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode is HttpStatusCode.NotFound or
                HttpStatusCode.Unauthorized or
                HttpStatusCode.Forbidden)
            {
                return new(SNEStorageResourceLookupStatus.NotFound);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "SNEStorage resource lookup for {ResourceId} returned HTTP {StatusCode}.",
                    resourceId,
                    (int)response.StatusCode);

                return new(SNEStorageResourceLookupStatus.Unavailable);
            }

            var resource = await response.Content
                .ReadFromJsonAsync<SNEStorageResourceDTO>(
                    cancellationToken: cancellationToken);

            if (resource is null || resource.Id != resourceId ||
                string.IsNullOrWhiteSpace(resource.Name))
            {
                return new(SNEStorageResourceLookupStatus.InvalidResponse);
            }

            return new(SNEStorageResourceLookupStatus.Success, resource);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            return new(SNEStorageResourceLookupStatus.Unavailable);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(
                exception,
                "SNEStorage resource lookup failed for resource {ResourceId}.",
                resourceId);

            return new(SNEStorageResourceLookupStatus.Unavailable);
        }
    }

    public async Task<SNEStorageMemberLookupResult> GetMemberStateAsync(
        ulong discordUserId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl) ||
            string.IsNullOrWhiteSpace(_options.BotApiKey) ||
            !Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var baseUri))
        {
            logger.LogError(
                "SNEStorage API configuration is missing. " +
                "SNEStorage:BaseUrl and SNEStorage:BotApiKey are required.");

            return new(SNEStorageMemberLookupStatus.ConfigurationError);
        }

        var endpoint = new Uri(
            baseUri,
            $"api/internal/discord/member-state/{discordUserId}");

        using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);
        request.Headers.TryAddWithoutValidation(BotKeyHeader, _options.BotApiKey);

        try
        {
            using var response = await httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
                return new(SNEStorageMemberLookupStatus.NotLinked);

            if (response.StatusCode == HttpStatusCode.Forbidden)
                return new(SNEStorageMemberLookupStatus.Ineligible);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                logger.LogError(
                    "SNEStorage rejected the Discord bot API key.");
                return new(SNEStorageMemberLookupStatus.Unauthorized);
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "SNEStorage member lookup for Discord user {DiscordUserId} " +
                    "returned HTTP {StatusCode}.",
                    discordUserId,
                    (int)response.StatusCode);

                return new(SNEStorageMemberLookupStatus.Unavailable);
            }

            var member = await response.Content
                .ReadFromJsonAsync<SNEStorageMemberStateDTO>(
                    cancellationToken: cancellationToken);

            if (member is null || string.IsNullOrWhiteSpace(member.SNEStorageUserId))
            {
                logger.LogError(
                    "SNEStorage returned an invalid member state for Discord user {DiscordUserId}.",
                    discordUserId);

                return new(SNEStorageMemberLookupStatus.InvalidResponse);
            }

            return new(SNEStorageMemberLookupStatus.Success, member);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(
                "SNEStorage member lookup timed out for Discord user {DiscordUserId}.",
                discordUserId);

            return new(SNEStorageMemberLookupStatus.Unavailable);
        }
        catch (HttpRequestException exception)
        {
            logger.LogError(
                exception,
                "SNEStorage member lookup failed for Discord user {DiscordUserId}.",
                discordUserId);

            return new(SNEStorageMemberLookupStatus.Unavailable);
        }
    }
}
