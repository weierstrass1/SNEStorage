using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using SNEStorage.DiscordBot.DTOs;

namespace SNEStorage.DiscordBot.Services;

public sealed class DiscordNewsNotificationService(
    DiscordSocketClient discordClient,
    IOptions<DiscordDTO> discordOptions,
    IOptions<SNEStorageApiDTO> apiOptions,
    SNEStorageApiClient apiClient,
    DiscordServerConfigLoader configLoader)
{
    private const int NewsBodyChunkLength = 3900;
    private static readonly Color NewsColor = new(0xB14CFF);

    private readonly DiscordDTO _discordOptions = discordOptions.Value;
    private readonly SNEStorageApiDTO _apiOptions = apiOptions.Value;

    public async Task<DiscordBotEventDispatchResult> PublishAsync(
        string entityId,
        DateTimeOffset occurredAtUtc,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(entityId, out var newsId) || newsId <= 0)
            return new(DiscordBotEventDispatchStatus.InvalidEvent);

        var lookup = await apiClient.GetNewsAsync(newsId, cancellationToken);

        if (lookup.Status == SNEStorageNewsLookupStatus.NotFound)
            return new(DiscordBotEventDispatchStatus.EntityNotFound);

        if (lookup.Status != SNEStorageNewsLookupStatus.Success ||
            lookup.News is null)
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
        var infoCategory = configuration.Categories.Single(category =>
            string.Equals(category.Key, "info", StringComparison.OrdinalIgnoreCase));
        var siteNews = infoCategory.Channels.Single(channel =>
            string.Equals(channel.Key, "site-news", StringComparison.OrdinalIgnoreCase));

        var category = guild.CategoryChannels.SingleOrDefault(existing =>
            string.Equals(existing.Name, infoCategory.Name, StringComparison.OrdinalIgnoreCase));
        var channel = guild.TextChannels.SingleOrDefault(existing =>
            existing.CategoryId == category?.Id &&
            string.Equals(existing.Name, siteNews.Name, StringComparison.OrdinalIgnoreCase));

        if (channel is null)
            return new(DiscordBotEventDispatchStatus.DiscordUnavailable);

        if (!Uri.TryCreate(
                _apiOptions.BaseUrl.TrimEnd('/') + "/",
                UriKind.Absolute,
                out var baseUri))
        {
            return new(DiscordBotEventDispatchStatus.SNEStorageUnavailable);
        }

        var news = lookup.News;
        var newsUrl = new Uri(baseUri, $"News/{news.Id}").ToString();
        var title = DiscordNotificationFormatting.SingleLine(
            news.Title,
            EmbedBuilder.MaxTitleLength,
            "SNEStorage update");
        var author = DiscordNotificationFormatting.SingleLine(
            news.UserSummary?.Username,
            256,
            "SNEStorage staff");
        var bodyChunks = DiscordNotificationFormatting.SplitBody(
            news.Text,
            NewsBodyChunkLength,
            "A new update was published on SNEStorage.");
        var timestamp = DiscordNotificationFormatting.Timestamp(
            news.CreatedAt,
            occurredAtUtc);
        var components = new ComponentBuilder()
            .WithButton(
                "Read on SNEStorage",
                style: ButtonStyle.Link,
                url: newsUrl)
            .Build();

        for (var index = 0; index < bodyChunks.Count; index++)
        {
            var continuation = index == 0
                ? title
                : DiscordNotificationFormatting.SingleLine(
                    $"{news.Title} · Continued {index + 1}/{bodyChunks.Count}",
                    EmbedBuilder.MaxTitleLength,
                    "SNEStorage update continued");

            var embedBuilder = new EmbedBuilder()
                .WithAuthor(index == 0 ? "SNEStorage News" : "SNEStorage News · Continued")
                .WithTitle(continuation)
                .WithUrl(newsUrl)
                .WithDescription(bodyChunks[index])
                .WithColor(NewsColor)
                .WithFooter($"News #{news.Id} · SNEStorage")
                .WithTimestamp(timestamp);

            if (index == 0)
            {
                embedBuilder.AddField("Published by", author, true);
            }

            await channel.SendMessageAsync(
                embed: embedBuilder.Build(),
                components: index == 0 ? components : null,
                allowedMentions: AllowedMentions.None);
        }

        return new(
            DiscordBotEventDispatchStatus.Success,
            ["published site news"]);
    }

}
