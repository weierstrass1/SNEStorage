using Discord;
using Discord.WebSocket;
using SNEStorage.DiscordBot.DTOs;

namespace SNEStorage.DiscordBot.Services;

public sealed class DiscordAutoModProvisioner(
    ILogger<DiscordAutoModProvisioner> logger)
{
    private const ulong DiscordAutoModSystemUserId = 1008776202191634432;

    private static readonly RequestOptions SetupRequest = new()
    {
        AuditLogReason = "SNEStorage server configuration setup"
    };

    public async Task ApplyAsync(
        SocketGuild guild,
        DiscordServerSetupConfigDTO configuration,
        IReadOnlyDictionary<string, IRole> configuredRoles,
        IReadOnlyDictionary<string, IGuildChannel> channels,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var autoMod = configuration.AutoMod;

        if (autoMod is null || autoMod.Rules.Length == 0)
            return;

        ValidateConfiguration(configuration, autoMod);

        if (!channels.TryGetValue(autoMod.AlertChannel, out var alertChannel))
        {
            throw new InvalidOperationException(
                $"AutoMod alert channel {autoMod.AlertChannel} was not created.");
        }

        var exemptRoleIds = ResolveExemptRoles(
            guild,
            configuration,
            autoMod.ExemptRoles,
            configuredRoles);

        var existingRules = await guild.GetAutoModRulesAsync(SetupRequest);

        foreach (var rule in autoMod.Rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var existing = existingRules.FirstOrDefault(existingRule =>
                string.Equals(existingRule.Name, rule.Name, StringComparison.OrdinalIgnoreCase));

            var triggerType = ParseTriggerType(rule.TriggerType);

            if (existing is not null && existing.TriggerType != triggerType)
            {
                throw new InvalidOperationException(
                    $"AutoMod rule {rule.Name} already exists with a different trigger type.");
            }

            if (existing is not null
                && ((IAutoModRule)existing).CreatorId == DiscordAutoModSystemUserId)
            {
                if (IsEquivalent(existing, rule, alertChannel.Id, exemptRoleIds))
                {
                    logger.LogInformation("Discord system AutoMod rule {RuleName} is already up to date.", rule.Name);
                }
                else
                {
                    logger.LogWarning(
                        "Discord system AutoMod rule {RuleName} cannot be changed through the bot API and was left unchanged.",
                        rule.Name);
                }

                continue;
            }

            if (existing is not null && IsEquivalent(existing, rule, alertChannel.Id, exemptRoleIds))
            {
                logger.LogInformation("AutoMod rule {RuleName} is already up to date.", rule.Name);
                continue;
            }

            if (existing is not null)
            {
                logger.LogInformation(
                    "AutoMod drift for {RuleName}: Id={RuleId}, GuildId={GuildId}, Enabled={Enabled}, " +
                    "MentionLimit={MentionLimit}, MentionRaidProtection={MentionRaidProtection}, " +
                    "ExemptRoles={ExemptRoles}, ExemptChannels={ExemptChannels}, Actions={Actions}.",
                    rule.Name,
                    existing.Id,
                    existing.Guild.Id,
                    existing.Enabled,
                    existing.MentionTotalLimit,
                    existing.MentionRaidProtectionEnabled,
                    string.Join(',', existing.ExemptRoles),
                    string.Join(',', existing.ExemptChannels),
                    string.Join(',', existing.Actions.Select(action => $"{action.Type}:{action.ChannelId}")));
            }

            logger.LogInformation(
                "{Mode} AutoMod rule {RuleName}.",
                dryRun ? "Would apply" : "Applying",
                rule.Name);

            if (dryRun)
                continue;

            try
            {
                if (existing is null)
                {
                    await guild.CreateAutoModRuleAsync(
                        properties => ApplyProperties(
                            properties,
                            rule,
                            alertChannel.Id,
                            exemptRoleIds,
                            includeTriggerType: true),
                        SetupRequest);
                }
                else
                {
                    await existing.ModifyAsync(
                        properties => ApplyProperties(
                            properties,
                            rule,
                            alertChannel.Id,
                            exemptRoleIds,
                            includeTriggerType: false),
                        SetupRequest);
                }
            }
            catch (Discord.Net.HttpException exception)
                when (exception.HttpCode == System.Net.HttpStatusCode.NotFound)
            {
                logger.LogWarning(
                    "Discord did not expose AutoMod rule {RuleName} for bot management; the rule was skipped.",
                    rule.Name);
            }
        }
    }

    private static bool IsEquivalent(
        IAutoModRule existing,
        DiscordAutoModRuleConfigDTO desired,
        ulong alertChannelId,
        ulong[] exemptRoleIds)
    {
        var desiredActions = BuildActions(desired.Actions, alertChannelId);
        var sameActions = existing.Actions.Count == desiredActions.Length
            && desiredActions.All(desiredAction => existing.Actions.Any(existingAction =>
                existingAction.Type == desiredAction.Type
                && existingAction.ChannelId == desiredAction.ChannelId));

        var sameMentionSettings = existing.TriggerType != AutoModTriggerType.MentionSpam
            || (existing.MentionTotalLimit == (desired.MaxUniqueMentions ?? 5)
                && existing.MentionRaidProtectionEnabled == true);

        return string.Equals(existing.Name, desired.Name, StringComparison.Ordinal)
            && existing.EventType == AutoModEventType.MessageSend
            && existing.TriggerType == ParseTriggerType(desired.TriggerType)
            && existing.Enabled == desired.Enabled
            && existing.ExemptRoles.ToHashSet().SetEquals(exemptRoleIds)
            && existing.ExemptChannels.Count == 0
            && sameActions
            && sameMentionSettings;
    }

    private static void ApplyProperties(
        AutoModRuleProperties properties,
        DiscordAutoModRuleConfigDTO rule,
        ulong alertChannelId,
        ulong[] exemptRoleIds,
        bool includeTriggerType)
    {
        properties.Name = rule.Name;
        properties.EventType = AutoModEventType.MessageSend;
        properties.Enabled = rule.Enabled;
        properties.Actions = BuildActions(rule.Actions, alertChannelId);
        properties.ExemptRoles = exemptRoleIds;
        properties.ExemptChannels = Array.Empty<ulong>();

        var triggerType = ParseTriggerType(rule.TriggerType);

        if (includeTriggerType)
            properties.TriggerType = triggerType;

        if (triggerType == AutoModTriggerType.MentionSpam)
        {
            properties.MentionLimit = rule.MaxUniqueMentions ?? 5;
            properties.MentionRaidProtectionEnabled = true;
        }
    }

    private static AutoModRuleActionProperties[] BuildActions(
        IEnumerable<string> configuredActions,
        ulong alertChannelId) =>
        [.. configuredActions.Select(action => action.ToUpperInvariant() switch
        {
            "BLOCK_MESSAGE" => new AutoModRuleActionProperties
            {
                Type = AutoModActionType.BlockMessage
            },
            "SEND_ALERT" or "SEND_ALERT_MESSAGE" => new AutoModRuleActionProperties
            {
                Type = AutoModActionType.SendAlertMessage,
                ChannelId = alertChannelId
            },
            _ => throw new InvalidOperationException($"Unknown AutoMod action: {action}.")
        })];

    private static ulong[] ResolveExemptRoles(
        SocketGuild guild,
        DiscordServerSetupConfigDTO configuration,
        IEnumerable<string> configuredKeys,
        IReadOnlyDictionary<string, IRole> configuredRoles)
    {
        var result = new List<ulong>();

        foreach (var key in configuredKeys)
        {
            if (key.Equals("bot", StringComparison.OrdinalIgnoreCase))
            {
                var botRole = guild.CurrentUser.Roles.FirstOrDefault(role =>
                        string.Equals(role.Name, configuration.Bot.RoleName, StringComparison.OrdinalIgnoreCase))
                    ?? guild.CurrentUser.Roles.FirstOrDefault(role => role.IsManaged);

                if (botRole is null)
                    throw new InvalidOperationException("The bot role could not be resolved for AutoMod.");

                result.Add(botRole.Id);
                continue;
            }

            if (!configuredRoles.TryGetValue(key, out var role))
                throw new InvalidOperationException($"Unknown AutoMod exempt role: {key}.");

            result.Add(role.Id);
        }

        return [.. result];
    }

    private static void ValidateConfiguration(
        DiscordServerSetupConfigDTO configuration,
        DiscordAutoModConfigDTO autoMod)
    {
        var validChannel = configuration.Categories.Any(category =>
            category.Channels.Any(channel =>
                string.Equals(
                    $"{category.Key}.{channel.Key}",
                    autoMod.AlertChannel,
                    StringComparison.OrdinalIgnoreCase)));

        if (!validChannel)
            throw new InvalidOperationException($"Unknown AutoMod alert channel: {autoMod.AlertChannel}.");

        var validRoles = configuration.Roles.Select(role => role.Key)
            .Append("bot")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (autoMod.ExemptRoles.Any(role => !validRoles.Contains(role)))
            throw new InvalidOperationException("AutoMod contains an unknown exempt role.");

        foreach (var rule in autoMod.Rules)
        {
            _ = ParseTriggerType(rule.TriggerType);
            _ = BuildActions(rule.Actions, 0);
        }
    }

    private static AutoModTriggerType ParseTriggerType(string value) =>
        value.ToUpperInvariant() switch
        {
            "SPAM" => AutoModTriggerType.Spam,
            "MENTION_SPAM" => AutoModTriggerType.MentionSpam,
            _ => throw new InvalidOperationException($"Unsupported AutoMod trigger: {value}.")
        };
}
