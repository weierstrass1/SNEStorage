using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using SNEStorage.DiscordBot.DTOs;

namespace SNEStorage.DiscordBot.Services;

public sealed class DiscordUserNotificationService(
    DiscordSocketClient discordClient,
    IOptions<DiscordDTO> discordOptions,
    IOptions<SNEStorageApiDTO> apiOptions,
    SNEStorageApiClient apiClient,
    DiscordServerConfigLoader configLoader)
{
    private readonly DiscordDTO _discordOptions = discordOptions.Value;
    private readonly SNEStorageApiDTO _apiOptions = apiOptions.Value;

    public async Task<DiscordBotEventDispatchResult> PublishRegisteredAsync(
        string userId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
            return new(DiscordBotEventDispatchStatus.InvalidEvent);

        var lookup = await apiClient.GetUserProfileAsync(
            userId,
            cancellationToken);

        if (lookup.Status == SNEStorageUserLookupStatus.NotFound)
            return new(DiscordBotEventDispatchStatus.EntityNotFound);

        if (lookup.Status != SNEStorageUserLookupStatus.Success ||
            lookup.User?.UserSummary is null)
        {
            return new(DiscordBotEventDispatchStatus.SNEStorageUnavailable);
        }

        if (discordClient.ConnectionState != ConnectionState.Connected ||
            !ulong.TryParse(_discordOptions.GuildId, out var guildId))
        {
            return new(DiscordBotEventDispatchStatus.DiscordUnavailable);
        }

        var guild = discordClient.GetGuild(guildId);
        if (guild is null)
            return new(DiscordBotEventDispatchStatus.DiscordUnavailable);

        var configuration = await configLoader.LoadAsync(cancellationToken);
        var staffCategory = configuration.Categories.Single(category =>
            string.Equals(category.Key, "staff", StringComparison.OrdinalIgnoreCase));
        var joinLog = staffCategory.Channels.Single(channel =>
            string.Equals(channel.Key, "join-log", StringComparison.OrdinalIgnoreCase));

        var category = guild.CategoryChannels.SingleOrDefault(existing =>
            string.Equals(existing.Name, staffCategory.Name, StringComparison.OrdinalIgnoreCase));
        var channel = guild.TextChannels.SingleOrDefault(existing =>
            existing.CategoryId == category?.Id &&
            string.Equals(existing.Name, joinLog.Name, StringComparison.OrdinalIgnoreCase));

        if (channel is null)
            return new(DiscordBotEventDispatchStatus.DiscordUnavailable);

        if (!Uri.TryCreate(
                _apiOptions.BaseUrl.TrimEnd('/') + "/",
                UriKind.Absolute,
                out var baseUri))
        {
            return new(DiscordBotEventDispatchStatus.SNEStorageUnavailable);
        }

        var profileUrl = new Uri(
            baseUri,
            $"Profile/{Uri.EscapeDataString(userId)}").ToString();

        var embed = new EmbedBuilder()
            .WithTitle(lookup.User.UserSummary.Username)
            .WithUrl(profileUrl)
            .WithDescription("A new user registered on SNEStorage.")
            .WithColor(new Color(0x57F287))
            .AddField("SNEStorage user ID", userId)
            .WithFooter("SNEStorage user log")
            .WithTimestamp(occurredAtUtc)
            .Build();

        await channel.SendMessageAsync(
            embed: embed,
            allowedMentions: AllowedMentions.None);

        return new(
            DiscordBotEventDispatchStatus.Success,
            ["published user registration log"]);
    }
}
