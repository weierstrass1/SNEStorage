using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using SNEStorage.DiscordBot.DTOs;

namespace SNEStorage.DiscordBot.Services;

public sealed class DiscordResourceNotificationService(
    DiscordSocketClient discordClient,
    IOptions<DiscordDTO> discordOptions,
    IOptions<SNEStorageApiDTO> apiOptions,
    SNEStorageApiClient apiClient,
    DiscordServerConfigLoader configLoader)
{
    private static readonly Color ResourceColor = new(0xB14CFF);

    private readonly DiscordDTO _discordOptions = discordOptions.Value;
    private readonly SNEStorageApiDTO _apiOptions = apiOptions.Value;

    public async Task<DiscordBotEventDispatchResult> PublishCreatedAsync(
        string entityId,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(entityId, out var resourceId) || resourceId <= 0)
            return new(DiscordBotEventDispatchStatus.InvalidEvent);

        var lookup = await apiClient.GetResourceAsync(
            resourceId,
            cancellationToken);

        if (lookup.Status == SNEStorageResourceLookupStatus.NotFound)
            return new(DiscordBotEventDispatchStatus.EntityNotFound);

        if (lookup.Status != SNEStorageResourceLookupStatus.Success ||
            lookup.Resource is null)
        {
            return new(DiscordBotEventDispatchStatus.SNEStorageUnavailable);
        }

        var resource = lookup.Resource;

        // Visibility 1 is public. Never announce private or unlisted resources.
        if (resource.Visibility.Id != 1)
        {
            return new(
                DiscordBotEventDispatchStatus.Success,
                ["ignored non-public resource"]);
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
        var resourceLog = infoCategory.Channels.Single(channel =>
            string.Equals(channel.Key, "resource-log", StringComparison.OrdinalIgnoreCase));

        var category = guild.CategoryChannels.SingleOrDefault(existing =>
            string.Equals(existing.Name, infoCategory.Name, StringComparison.OrdinalIgnoreCase));
        var channel = guild.TextChannels.SingleOrDefault(existing =>
            existing.CategoryId == category?.Id &&
            string.Equals(existing.Name, resourceLog.Name, StringComparison.OrdinalIgnoreCase));

        if (channel is null)
            return new(DiscordBotEventDispatchStatus.DiscordUnavailable);

        if (!Uri.TryCreate(
                _apiOptions.BaseUrl.TrimEnd('/') + "/",
                UriKind.Absolute,
                out var baseUri))
        {
            return new(DiscordBotEventDispatchStatus.SNEStorageUnavailable);
        }

        var resourceUrl = new Uri(baseUri, $"Resource/{resource.Id}").ToString();
        var title = DiscordNotificationFormatting.SingleLine(
            resource.Name,
            EmbedBuilder.MaxTitleLength,
            "New SNEStorage resource");
        var description = DiscordNotificationFormatting.Body(
            resource.Description,
            2200,
            "A new resource has been published on SNEStorage.");
        var game = DiscordNotificationFormatting.SingleLine(
            resource.Videogame.Name,
            EmbedFieldBuilder.MaxFieldValueLength,
            "Unknown");
        var types = DiscordNotificationFormatting.JoinValues(
            resource.Types.Select(type => type.Name),
            700,
            "Uncategorized");
        var tags = DiscordNotificationFormatting.JoinValues(
            resource.Tags.Select(tag => $"#{tag.Name.Trim().TrimStart('#')}"),
            700,
            string.Empty);

        var componentBuilder = new ComponentBuilder()
            .WithButton(
                "View resource",
                style: ButtonStyle.Link,
                url: resourceUrl);

        if (Uri.TryCreate(resource.RepositoryURL, UriKind.Absolute, out var repositoryUri) &&
            repositoryUri.Scheme is "http" or "https")
        {
            componentBuilder.WithButton(
                "Source repository",
                style: ButtonStyle.Link,
                url: repositoryUri.ToString());
        }

        var embedBuilder = new EmbedBuilder()
            .WithAuthor("New SNEStorage Resource")
            .WithTitle(title)
            .WithUrl(resourceUrl)
            .WithDescription(description)
            .WithColor(ResourceColor)
            .AddField("🎮 Game", game, true)
            .AddField("🧩 Types", types, true);

        if (!string.IsNullOrWhiteSpace(tags))
            embedBuilder.AddField("🏷️ Tags", tags);

        var embed = embedBuilder
            .WithFooter($"Resource #{resource.Id} · SNEStorage")
            .WithTimestamp(DiscordNotificationFormatting.Timestamp(
                resource.CreatedAt,
                DateTimeOffset.UtcNow))
            .Build();

        await channel.SendMessageAsync(
            embed: embed,
            components: componentBuilder.Build(),
            allowedMentions: AllowedMentions.None);

        return new(
            DiscordBotEventDispatchStatus.Success,
            ["published resource log"]);
    }
}
