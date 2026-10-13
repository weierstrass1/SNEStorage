namespace SNEStorage.DiscordBot.DTOs;

public static class DiscordBotEventTypes
{
    public const string MemberChanged = "member.changed";
    public const string UserRegistered = "user.registered";
    public const string ResourceCreated = "resource.created";
    public const string ResourceSealGranted = "resource.seal-of-quality-granted";
    public const string NewsPublished = "news.published";
}

public sealed record DiscordBotEventDTO(
    Guid EventId,
    string Type,
    string EntityId,
    DateTimeOffset OccurredAtUtc);

public enum DiscordBotEventDispatchStatus
{
    Success,
    InvalidEvent,
    DiscordUnavailable,
    EntityNotFound,
    Ineligible,
    SNEStorageUnavailable,
    UnsupportedEvent
}

public sealed record DiscordBotEventDispatchResult(
    DiscordBotEventDispatchStatus Status,
    IReadOnlyList<string>? Changes = null);
