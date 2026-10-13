namespace SNEStorage.DiscordBot.DTOs;

public sealed class SNEStorageApiDTO
{
    public string BaseUrl { get; init; } = "https://snestorage.dev/";
    public string BotApiKey { get; init; } = string.Empty;
    public string AccountLinkUrl { get; init; } = "https://snestorage.dev/api/discord/link/start";
}

public sealed record SNEStorageMemberStateDTO(
    string? SNEStorageUserId,
    string? Nickname,
    string? Color,
    bool IsModerator,
    bool IsSupporter);

public enum SNEStorageMemberLookupStatus
{
    Success,
    NotLinked,
    Ineligible,
    Unauthorized,
    Unavailable,
    InvalidResponse,
    ConfigurationError
}

public sealed record SNEStorageMemberLookupResult(
    SNEStorageMemberLookupStatus Status,
    SNEStorageMemberStateDTO? Member = null);

public sealed class SNEStorageResourceDTO
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string? RepositoryURL { get; init; }
    public SNEStorageIdNameDTO Videogame { get; init; } = new();
    public SNEStorageIdNameDTO Visibility { get; init; } = new();
    public IReadOnlyList<SNEStorageResourceTypeDTO> Types { get; init; } = [];
    public IReadOnlyList<SNEStorageResourceTagDTO> Tags { get; init; } = [];
}

public sealed class SNEStorageIdNameDTO
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed record SNEStorageResourceTypeDTO(
    long Id,
    long ResourceTypeId,
    string Name,
    long CategoryId,
    string CategoryName);

public sealed record SNEStorageResourceTagDTO(
    long Id,
    long TagId,
    string Name);

public enum SNEStorageResourceLookupStatus
{
    Success,
    NotFound,
    Unavailable,
    InvalidResponse,
    ConfigurationError
}

public sealed record SNEStorageResourceLookupResult(
    SNEStorageResourceLookupStatus Status,
    SNEStorageResourceDTO? Resource = null);

public sealed class SNEStorageUserProfileDTO
{
    public string UserId { get; init; } = string.Empty;
    public SNEStorageUserSummaryDTO? UserSummary { get; init; }
}

public sealed class SNEStorageUserSummaryDTO
{
    public string Id { get; init; } = string.Empty;
    public string Username { get; init; } = string.Empty;
}

public enum SNEStorageUserLookupStatus
{
    Success,
    NotFound,
    Unavailable,
    InvalidResponse,
    ConfigurationError
}

public sealed record SNEStorageUserLookupResult(
    SNEStorageUserLookupStatus Status,
    SNEStorageUserProfileDTO? User = null);

public sealed class SNEStorageNewsDTO
{
    public long Id { get; init; }
    public DateTime CreatedAt { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public SNEStorageUserSummaryDTO? UserSummary { get; init; }
}

public enum SNEStorageNewsLookupStatus
{
    Success,
    NotFound,
    Unavailable,
    InvalidResponse,
    ConfigurationError
}

public sealed record SNEStorageNewsLookupResult(
    SNEStorageNewsLookupStatus Status,
    SNEStorageNewsDTO? News = null);
