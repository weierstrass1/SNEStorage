namespace SNEStorage.DiscordBot.DTOs;

public sealed class DiscordServerSetupConfigDTO
{
    public int SchemaVersion { get; init; }
    public required DiscordGuildConfigDTO Server { get; init; }
    public required DiscordBotConfigDTO Bot { get; init; }
    public DiscordRoleConfigDTO[] Roles { get; init; } = [];
    public Dictionary<string, DiscordPermissionOverwriteConfigDTO[]> PermissionProfiles { get; init; }
        = new(StringComparer.OrdinalIgnoreCase);
    public DiscordCategoryConfigDTO[] Categories { get; init; } = [];
    public DiscordNativeSafetyConfigDTO? NativeSafety { get; init; }
    public DiscordAutoModConfigDTO? AutoMod { get; init; }
    public DiscordReconciliationConfigDTO Reconciliation { get; init; } = new();
}

public sealed class DiscordGuildConfigDTO
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public string? PreferredLocale { get; init; }
    public string? IconPath { get; init; }
    public bool CommunityEnabled { get; init; }
    public string? DefaultMessageNotifications { get; init; }
    public string? ExplicitContentFilter { get; init; }
    public string? VerificationLevel { get; init; }
    public int AfkTimeoutSeconds { get; init; } = 300;
}

public sealed class DiscordBotConfigDTO
{
    public required string RoleName { get; init; }
}

public sealed class DiscordPermissionOverwriteConfigDTO
{
    public required string Target { get; init; }
    public string[] Allow { get; init; } = [];
    public string[] Deny { get; init; } = [];
}

public sealed class DiscordNativeSafetyConfigDTO
{
    public DiscordRulesScreeningConfigDTO? RulesScreening { get; init; }
    public DiscordRaidProtectionConfigDTO? RaidProtection { get; init; }
}

public sealed class DiscordRulesScreeningConfigDTO
{
    public bool Enabled { get; init; }
    public required string RulesChannel { get; init; }
}

public sealed class DiscordRaidProtectionConfigDTO
{
    public bool Enabled { get; init; }
    public required string AlertsChannel { get; init; }
}

public sealed class DiscordAutoModConfigDTO
{
    public required string AlertChannel { get; init; }
    public string[] ExemptRoles { get; init; } = [];
    public DiscordAutoModRuleConfigDTO[] Rules { get; init; } = [];
}

public sealed class DiscordAutoModRuleConfigDTO
{
    public required string Key { get; init; }
    public required string Name { get; init; }
    public bool Enabled { get; init; }
    public required string TriggerType { get; init; }
    public int? MaxUniqueMentions { get; init; }
    public string[] Actions { get; init; } = [];
}

public sealed class DiscordReconciliationConfigDTO
{
    public bool DeleteUnknownChannels { get; init; }
    public bool DeleteUnknownCategories { get; init; }
}
