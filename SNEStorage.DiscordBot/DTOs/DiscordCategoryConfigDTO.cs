namespace SNEStorage.DiscordBot.DTOs;

public sealed class DiscordCategoryConfigDTO
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public string[] PreviousNames { get; init; } = [];
    public required string PermissionProfile { get; init; }
    public DiscordChannelConfigDTO[] Channels { get; init; } = [];
}
public sealed class DiscordChannelConfigDTO
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Type { get; init; }
    public string[] PreviousNames { get; init; } = [];

    public string? Topic { get; init; }
    public int? SlowmodeSeconds { get; init; }
    public int? DefaultThreadSlowmodeSeconds { get; init; }
    public int? UserLimit { get; init; }
    public int? Bitrate { get; init; }

    public string? DefaultReactionEmoji { get; init; }
    public string? DefaultSortOrder { get; init; }
    public string? DefaultForumLayout { get; init; }

    public string[] AvailableTags { get; init; } = [];
    public DiscordInitialMessageConfigDTO[] InitialMessages { get; init; } = [];
}
public sealed class DiscordInitialMessageConfigDTO
{
    public bool Pin { get; init; }
    public required string Content { get; init; }
}
