using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using SNEStorage.DiscordBot.DTOs;

namespace SNEStorage.DiscordBot.Services;

public sealed class DiscordBotEventDispatcher(
    DiscordSocketClient discordClient,
    IOptions<DiscordDTO> discordOptions,
    DiscordAccessService accessService,
    DiscordResourceNotificationService resourceNotifications,
    DiscordUserNotificationService userNotifications,
    DiscordNewsNotificationService newsNotifications)
{
    private readonly DiscordDTO _discordOptions = discordOptions.Value;

    public async Task<DiscordBotEventDispatchResult> DispatchAsync(
        DiscordBotEventDTO notification,
        CancellationToken cancellationToken)
    {
        if (notification.EventId == Guid.Empty ||
            string.IsNullOrWhiteSpace(notification.Type) ||
            string.IsNullOrWhiteSpace(notification.EntityId) ||
            notification.OccurredAtUtc == default)
        {
            return new(DiscordBotEventDispatchStatus.InvalidEvent);
        }

        return notification.Type.Trim().ToLowerInvariant() switch
        {
            DiscordBotEventTypes.MemberChanged =>
                await SynchronizeMemberAsync(
                    notification.EntityId,
                    cancellationToken),

            DiscordBotEventTypes.ResourceCreated =>
                await resourceNotifications.PublishCreatedAsync(
                    notification.EntityId,
                    cancellationToken),

            DiscordBotEventTypes.UserRegistered =>
                await userNotifications.PublishRegisteredAsync(
                    notification.EntityId,
                    notification.OccurredAtUtc,
                    cancellationToken),

            DiscordBotEventTypes.NewsPublished =>
                await newsNotifications.PublishAsync(
                    notification.EntityId,
                    notification.OccurredAtUtc,
                    cancellationToken),

            DiscordBotEventTypes.ResourceSealGranted =>
                new(DiscordBotEventDispatchStatus.UnsupportedEvent),

            _ => new(DiscordBotEventDispatchStatus.UnsupportedEvent)
        };
    }

    private async Task<DiscordBotEventDispatchResult> SynchronizeMemberAsync(
        string entityId,
        CancellationToken cancellationToken)
    {
        if (!ulong.TryParse(entityId, out var discordUserId))
            return new(DiscordBotEventDispatchStatus.InvalidEvent);

        if (discordClient.ConnectionState != ConnectionState.Connected ||
            !ulong.TryParse(_discordOptions.GuildId, out var guildId))
        {
            return new(DiscordBotEventDispatchStatus.DiscordUnavailable);
        }

        var guild = discordClient.GetGuild(guildId);
        if (guild is null)
            return new(DiscordBotEventDispatchStatus.DiscordUnavailable);

        var user = guild.GetUser(discordUserId);
        if (user is null)
        {
            await guild.DownloadUsersAsync();
            cancellationToken.ThrowIfCancellationRequested();
            user = guild.GetUser(discordUserId);
        }

        if (user is null)
            return new(DiscordBotEventDispatchStatus.EntityNotFound);

        var synchronization = await accessService.SynchronizeMemberAsync(
            user,
            cancellationToken);

        return synchronization.Status switch
        {
            SNEStorageMemberLookupStatus.Success => new(
                DiscordBotEventDispatchStatus.Success,
                synchronization.Changes),

            SNEStorageMemberLookupStatus.NotLinked =>
                new(DiscordBotEventDispatchStatus.EntityNotFound),

            SNEStorageMemberLookupStatus.Ineligible =>
                new(DiscordBotEventDispatchStatus.Ineligible),

            _ => new(DiscordBotEventDispatchStatus.SNEStorageUnavailable)
        };
    }
}
