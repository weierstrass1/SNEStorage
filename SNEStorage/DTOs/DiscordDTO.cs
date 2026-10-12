namespace SNEStorage.Discord;

public sealed class DiscordDTO
{
    public const string SectionName = "Discord";
    public required bool Enabled { get; init; }
    public required string ApplicationId { get; init; }
    public required string GuildId { get; init; }
    public required string ClientSecret { get; init; }
    public required string BotToken { get; init; }
    public required string PublicBaseUrl { get; init; } = "https://snestorage.dev";
    public required string OAuthCallbackPath { get; init; } = "/api/discord/link/callback";
    public required string BotApiKey { get; init; }
}
public sealed record DiscordMemberStateDTO(bool Eligible, string Reason, string? SNEStorageUserId,
    string? Nickname, string? Color, bool IsModerator, bool IsSupporter);
