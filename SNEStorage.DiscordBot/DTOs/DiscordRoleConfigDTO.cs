namespace SNEStorage.DiscordBot.DTOs;
public sealed class DiscordRoleConfigDTO
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public required string Color { get; init; } = "#FFFFFF";
    public required bool Hoist { get; init; }
    public required bool Mentionable { get; init; }
    public bool AssignToGuildOwner { get; init; }
    public required string[] Permissions { get; init; } = [];
}
