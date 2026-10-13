using System.Collections.Concurrent;
using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using SNEStorage.DiscordBot.DTOs;

namespace SNEStorage.DiscordBot.Services;

public sealed record DiscordMemberSynchronizationResult(
    SNEStorageMemberLookupStatus Status,
    IReadOnlyList<string> Changes);

public sealed class DiscordAccessService(
    SNEStorageApiClient apiClient,
    DiscordMemberSynchronizer memberSynchronizer,
    DiscordServerConfigLoader configLoader,
    IOptions<SNEStorageApiDTO> options,
    ILogger<DiscordAccessService> logger)
{
    private sealed record VerificationResponse(
        string Content,
        MessageComponent? Components = null);

    public const string AllowAccessButtonId = "snestorage:allow-access:v2";
    private const string LegacyAllowAccessButtonId = "snestorage:allow-access:v1";
    private const string RulesMessageMarker = "# SNEStorage Community Rules";
    private const string LegacyVerificationMessageMarker = "## SNEStorage access verification";
    private readonly SNEStorageApiDTO _options = options.Value;
    private readonly ConcurrentDictionary<ulong, SemaphoreSlim> _memberLocks = new();

    public async Task EnsureAccessMessageAsync(
        SocketGuild guild,
        CancellationToken cancellationToken = default)
    {
        var configuration = await configLoader.LoadAsync(cancellationToken);
        var infoCategory = configuration.Categories.Single(category =>
            string.Equals(category.Key, "info", StringComparison.OrdinalIgnoreCase));
        var rulesConfig = infoCategory.Channels.Single(channel =>
            string.Equals(channel.Key, "rules", StringComparison.OrdinalIgnoreCase));

        var category = guild.CategoryChannels.SingleOrDefault(existing =>
            string.Equals(existing.Name, infoCategory.Name, StringComparison.OrdinalIgnoreCase));
        var channel = guild.TextChannels.SingleOrDefault(existing =>
            existing.CategoryId == category?.Id &&
            string.Equals(existing.Name, rulesConfig.Name, StringComparison.OrdinalIgnoreCase));

        if (channel is null)
        {
            throw new InvalidOperationException(
                "The configured Discord rules channel does not exist. Run setup first.");
        }

        var configuredRulesMessage = rulesConfig.InitialMessages.SingleOrDefault()
            ?? throw new InvalidOperationException(
                "The configured Discord rules channel must contain exactly one initial message.");

        var messageContent = configuredRulesMessage.Content;
        var components = BuildAccessButton();
        IMessageChannel messageChannel = channel;
        var recentMessages = await messageChannel
            .GetMessagesAsync(100, CacheMode.AllowDownload)
            .FlattenAsync();

        var managedMessages = recentMessages
            .OfType<IUserMessage>()
            .Where(existing =>
                existing.Author.Id == guild.CurrentUser.Id &&
                IsManagedRulesMessage(existing))
            .ToArray();

        var message = managedMessages.FirstOrDefault(existing =>
                existing.Content.StartsWith(RulesMessageMarker, StringComparison.Ordinal))
            ?? managedMessages.FirstOrDefault();

        if (message is null)
        {
            message = await channel.SendMessageAsync(
                messageContent,
                allowedMentions: AllowedMentions.None,
                components: components);
        }
        else
        {
            await message.ModifyAsync(properties =>
            {
                properties.Content = messageContent;
                properties.Components = components;
                properties.AllowedMentions = AllowedMentions.None;
            });
        }

        if (!message.IsPinned)
            await message.PinAsync();

        foreach (var duplicate in managedMessages.Where(existing => existing.Id != message.Id))
            await duplicate.DeleteAsync();
    }

    public async Task HandleButtonAsync(SocketMessageComponent component)
    {
        if (!string.Equals(
                component.Data.CustomId,
                AllowAccessButtonId,
                StringComparison.Ordinal))
        {
            return;
        }

        await component.DeferLoadingAsync(ephemeral: true);

        VerificationResponse response;
        try
        {
            response = component.User is SocketGuildUser guildUser
                ? await VerifyAndSynchronizeAsync(guildUser)
                : new("I couldn't identify your member account in this Discord server.");
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Discord access verification failed for user {DiscordUserId}.",
                component.User.Id);

            response = new(
                "I couldn't finish the verification because of an internal error. " +
                "Please try again later or contact a moderator.");
        }
        await component.ModifyOriginalResponseAsync(properties =>
        {
            properties.Content = response.Content;
            if (response.Components is not null)
                properties.Components = response.Components;
        });
    }

    public async Task HandleUserJoinedAsync(SocketGuildUser user)
    {
        try
        {
            await SynchronizeMemberAsync(user);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Automatic Discord access synchronization failed for new member {DiscordUserId}.",
                user.Id);
        }
    }

    public async Task<DiscordMemberSynchronizationResult> SynchronizeMemberAsync(
        SocketGuildUser user,
        CancellationToken cancellationToken = default)
    {
        var memberLock = _memberLocks.GetOrAdd(
            user.Id,
            _ => new SemaphoreSlim(1, 1));

        await memberLock.WaitAsync(cancellationToken);

        try
        {
            var lookup = await apiClient.GetMemberStateAsync(
                user.Id,
                cancellationToken);

            if (lookup.Status != SNEStorageMemberLookupStatus.Success ||
                lookup.Member is null)
            {
                return new(lookup.Status, []);
            }

            var synchronization = await memberSynchronizer.ApplyAsync(
                user,
                lookup.Member,
                cancellationToken);

            return new(lookup.Status, synchronization.Changes);
        }
        finally
        {
            memberLock.Release();
        }
    }

    private async Task<VerificationResponse> VerifyAndSynchronizeAsync(
        SocketGuildUser guildUser)
    {
        var synchronization = await SynchronizeMemberAsync(guildUser);

        if (synchronization.Status == SNEStorageMemberLookupStatus.Success)
        {
            return new(synchronization.Changes.Count == 0
                ? "✅ Your SNEStorage account is verified and your Discord access is already up to date."
                : "✅ Access granted. Your SNEStorage Member role, profile color, nickname, " +
                  "and eligible community roles have been synchronized.");
        }

        return synchronization.Status switch
        {
            SNEStorageMemberLookupStatus.NotLinked =>
                new VerificationResponse(
                    "Your Discord account is not linked to SNEStorage yet. " +
                    "Use the button below to link it, then press **Allow Access** again.",
                    BuildAccountLinkButton()),

            SNEStorageMemberLookupStatus.Ineligible =>
                new VerificationResponse(
                    "Your linked SNEStorage account cannot access the community. " +
                    "Confirm your email and make sure the account is active and not banned."),

            SNEStorageMemberLookupStatus.Unauthorized or
            SNEStorageMemberLookupStatus.ConfigurationError =>
                new VerificationResponse(
                    "The bot is not authorized to verify SNEStorage accounts. " +
                    "Please contact an administrator."),

            _ =>
                new VerificationResponse(
                    "SNEStorage is temporarily unavailable. Please try again in a moment.")
        };
    }

    private MessageComponent BuildAccountLinkButton() =>
        new ComponentBuilder()
            .WithButton(
                "Link SNEStorage account",
                style: ButtonStyle.Link,
                url: _options.AccountLinkUrl)
            .Build();

    private static bool IsManagedRulesMessage(IUserMessage message) =>
        message.Content.StartsWith(RulesMessageMarker, StringComparison.Ordinal) ||
        message.Content.StartsWith(LegacyVerificationMessageMarker, StringComparison.Ordinal) ||
        message.Components
            .OfType<ActionRowComponent>()
            .SelectMany(row => row.Components)
            .OfType<ButtonComponent>()
            .Any(button =>
                string.Equals(button.CustomId, AllowAccessButtonId, StringComparison.Ordinal) ||
                string.Equals(button.CustomId, LegacyAllowAccessButtonId, StringComparison.Ordinal));

    private static MessageComponent BuildAccessButton() =>
        new ComponentBuilder()
            .WithButton(
                "Allow Access",
                AllowAccessButtonId,
                ButtonStyle.Success,
                new Emoji("✅"))
            .Build();
}
