using System;
using System.Collections.Generic;
using System.Text;

namespace SNEStorage.DiscordBot.DTOs;
public sealed class DiscordDTO
{
    public required string ApplicationId { get; init; }
    public required string GuildId { get; init; }
    public required string BotToken { get; init; }
    public required string ServerConfigPath { get; init; } = "Config/server-config.json";
}
public sealed record BotRunMode(
    bool Setup,
    bool DryRun,
    bool PermissionsOnly,
    bool ForumTagsOnly);
