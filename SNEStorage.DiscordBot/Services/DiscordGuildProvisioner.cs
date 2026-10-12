using System.Net.Http.Headers;
using System.Net.Http.Json;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using SNEStorage.DiscordBot.DTOs;

namespace SNEStorage.DiscordBot.Services;

public sealed class DiscordGuildProvisioner(
    HttpClient httpClient,
    IOptions<DiscordDTO> options,
    DiscordServerConfigLoader configLoader,
    ILogger<DiscordGuildProvisioner> logger)
{
    public async Task ApplyAsync(
        SocketGuild guild,
        DiscordServerSetupConfigDTO configuration,
        IReadOnlyDictionary<string, IGuildChannel> channels,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var wantsCommunity = configuration.Server.CommunityEnabled;
        var hasCommunity = HasFeature(guild, GuildFeature.Community);

        if (wantsCommunity && !hasCommunity && !guild.CurrentUser.GuildPermissions.Administrator)
        {
            throw new InvalidOperationException(
                "Discord requires the bot to have Administrator permission to enable Community features.");
        }

        var rulesReference = configuration.NativeSafety?.RulesScreening?.RulesChannel;
        var updatesReference = configuration.NativeSafety?.RaidProtection?.AlertsChannel;

        if (wantsCommunity)
        {
            ValidateConfiguredReference(configuration, rulesReference, "rules");
            ValidateConfiguredReference(configuration, updatesReference, "community updates");
        }

        var iconPath = string.IsNullOrWhiteSpace(configuration.Server.IconPath)
            ? null
            : configLoader.ResolveConfigRelativePath(configuration.Server.IconPath);

        if (iconPath is not null && !File.Exists(iconPath))
        {
            throw new FileNotFoundException("The configured Discord server icon was not found.", iconPath);
        }

        logger.LogInformation(
            "{Mode} Discord server settings{CommunityAction}.",
            dryRun ? "Would apply" : "Applying",
            wantsCommunity && !hasCommunity ? " and enable Community" : string.Empty);

        if (dryRun)
            return;

        var rulesChannelId = ResolveConfiguredChannel(
            rulesReference,
            channels,
            wantsCommunity,
            "rules");

        var updatesChannelId = ResolveConfiguredChannel(
            updatesReference,
            channels,
            wantsCommunity,
            "community updates");

        var payload = new Dictionary<string, object?>
        {
            ["name"] = configuration.Server.Name,
            ["description"] = configuration.Server.Description,
            ["preferred_locale"] = configuration.Server.PreferredLocale ?? "en-US",
            ["verification_level"] = ParseVerificationLevel(configuration.Server.VerificationLevel),
            ["default_message_notifications"] = ParseDefaultNotifications(
                configuration.Server.DefaultMessageNotifications),
            ["explicit_content_filter"] = ParseExplicitContentFilter(
                configuration.Server.ExplicitContentFilter),
            ["afk_timeout"] = configuration.Server.AfkTimeoutSeconds
        };

        if (iconPath is not null)
        {
            var iconBytes = await File.ReadAllBytesAsync(iconPath, cancellationToken);
            payload["icon"] = $"data:image/png;base64,{Convert.ToBase64String(iconBytes)}";
        }

        if (wantsCommunity)
        {
            payload["rules_channel_id"] = rulesChannelId!.Value.ToString();
            payload["public_updates_channel_id"] = updatesChannelId!.Value.ToString();

            if (!hasCommunity)
                payload["features"] = BuildMutableFeatures(guild, includeCommunity: true);
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"https://discord.com/api/v10/guilds/{guild.Id}")
        {
            Content = JsonContent.Create(payload)
        };

        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bot",
            options.Value.BotToken);

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Discord rejected the server settings ({(int)response.StatusCode}): {responseBody}");
        }
    }

    private static ulong? ResolveConfiguredChannel(
        string? reference,
        IReadOnlyDictionary<string, IGuildChannel> channels,
        bool required,
        string purpose)
    {
        if (!string.IsNullOrWhiteSpace(reference) &&
            channels.TryGetValue(reference, out var channel))
        {
            return channel.Id;
        }

        if (required)
        {
            throw new InvalidOperationException(
                $"The Discord configuration does not resolve a {purpose} channel ({reference}).");
        }

        return null;
    }

    private static void ValidateConfiguredReference(
        DiscordServerSetupConfigDTO configuration,
        string? reference,
        string purpose)
    {
        var exists = configuration.Categories.Any(category =>
            category.Channels.Any(channel =>
                string.Equals(
                    $"{category.Key}.{channel.Key}",
                    reference,
                    StringComparison.OrdinalIgnoreCase)));

        if (!exists)
        {
            throw new InvalidOperationException(
                $"The Discord configuration references an unknown {purpose} channel ({reference}).");
        }
    }

    private static string[] BuildMutableFeatures(SocketGuild guild, bool includeCommunity)
    {
        var features = new List<string>();

        if (includeCommunity || HasFeature(guild, GuildFeature.Community))
            features.Add("COMMUNITY");
        if (HasFeature(guild, GuildFeature.Discoverable))
            features.Add("DISCOVERABLE");
        if (HasFeature(guild, GuildFeature.InvitesDisabled))
            features.Add("INVITES_DISABLED");
        if (HasFeature(guild, GuildFeature.RaidAlertsDisabled))
            features.Add("RAID_ALERTS_DISABLED");

        return [.. features];
    }

    private static bool HasFeature(SocketGuild guild, GuildFeature feature) =>
        (guild.Features.Value & feature) == feature;

    private static int ParseVerificationLevel(string? value) =>
        value?.ToUpperInvariant() switch
        {
            null or "NONE" => 0,
            "LOW" => 1,
            "MEDIUM" => 2,
            "HIGH" => 3,
            "VERY_HIGH" or "EXTREME" => 4,
            _ => throw new InvalidOperationException($"Unknown verification level: {value}.")
        };

    private static int ParseDefaultNotifications(string? value) =>
        value?.ToUpperInvariant() switch
        {
            null or "ALL_MESSAGES" => 0,
            "ONLY_MENTIONS" or "MENTIONS_ONLY" => 1,
            _ => throw new InvalidOperationException($"Unknown notification setting: {value}.")
        };

    private static int ParseExplicitContentFilter(string? value) =>
        value?.ToUpperInvariant() switch
        {
            null or "DISABLED" => 0,
            "MEMBERS_WITHOUT_ROLES" => 1,
            "ALL_MEMBERS" => 2,
            _ => throw new InvalidOperationException($"Unknown explicit content filter: {value}.")
        };
}
