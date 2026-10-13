using SNEStorage.Services;
using System.Text.Json.Serialization;

namespace SNEStorage.Discord;

public sealed class DiscordDTO
{
    public required string BotBaseUrl { get; init; } = "https://bot.snestorage.dev";
    public const string SectionName = "Discord";
    public required bool Enabled { get; init; }
    public required string ApplicationId { get; init; }
    public required string GuildId { get; init; }
    public required string ClientSecret { get; init; }
    public required string PublicBaseUrl { get; init; } = "https://snestorage.dev";
    public required string OAuthCallbackPath { get; init; } = "/api/discord/link/callback";
    public required string InviteUrl { get; init; } = "https://discord.gg/e8bHBcRKX9";
    public required string BotApiKey { get; init; }
}
public sealed record DiscordMemberStateDTO(string? SNEStorageUserId, string? Nickname, 
    string? Color, bool IsModerator, bool IsSupporter);
public sealed class DiscordOAuthTokenDTO
{
    [JsonPropertyName("access_token")]
    public string? AccessToken { get; init; }
}
public sealed class DiscordOAuthUserDTO
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("username")]
    public string? Username { get; init; }
}
public static class DiscordBotEventTypes
{
    public const string MemberChanged = "member.changed";
    public const string UserRegistered = "user.registered";
    public const string ResourceCreated = "resource.created";
    public const string ResourceSealGranted = "resource.seal-of-quality-granted";
    public const string NewsPublished = "news.published";
}
public sealed record DiscordBotEventDTO(Guid EventId, string Type, string EntityId, 
    DateTimeOffset OccurredAtUtc);
