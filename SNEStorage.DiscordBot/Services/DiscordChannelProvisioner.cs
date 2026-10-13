using Discord;
using Discord.Net;
using Discord.WebSocket;
using SNEStorage.DiscordBot.DTOs;

namespace SNEStorage.DiscordBot.Services;

public sealed class DiscordChannelProvisioner(
    DiscordGuildProvisioner guildProvisioner,
    ILogger<DiscordChannelProvisioner> logger)
{
    private static readonly RequestOptions SetupRequest = new()
    {
        AuditLogReason = "SNEStorage server configuration setup"
    };

    public async Task<IReadOnlyDictionary<string, IGuildChannel>> ApplyAsync(
        SocketGuild guild,
        DiscordServerSetupConfigDTO configuration,
        IReadOnlyDictionary<string, IRole> configuredRoles,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        ValidatePermissionProfiles(configuration);

        if (dryRun)
        {
            LogPlan(guild, configuration);
            LogReconciliationPlan(guild, configuration);
            await guildProvisioner.ApplyAsync(
                guild,
                configuration,
                BuildExistingChannelMap(guild, configuration),
                dryRun: true,
                cancellationToken);
            return BuildExistingChannelMap(guild, configuration);
        }

        var roleTargets = BuildRoleTargets(guild, configuration, configuredRoles);
        var categories = await EnsureCategoriesAsync(
            guild,
            configuration,
            roleTargets);

        var channels = await EnsureChannelsAsync(
            guild,
            configuration,
            categories,
            roleTargets,
            includeCommunityChannels: false);

        await guildProvisioner.ApplyAsync(
            guild,
            configuration,
            channels,
            dryRun: false,
            cancellationToken);

        channels = await EnsureChannelsAsync(
            guild,
            configuration,
            categories,
            roleTargets,
            includeCommunityChannels: true,
            channels);

        await EnsureInitialMessagesAsync(configuration, channels, guild.CurrentUser.Id);

        await ReconcileUnknownChannelsAsync(
            guild,
            configuration,
            categories,
            channels);

        return channels;
    }

    public async Task ApplyPermissionsOnlyAsync(
        SocketGuild guild,
        DiscordServerSetupConfigDTO configuration,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        ValidatePermissionProfiles(configuration);

        var configuredRoles = configuration.Roles.ToDictionary(
            role => role.Key,
            role => (IRole)(guild.Roles.SingleOrDefault(existing =>
                string.Equals(existing.Name, role.Name, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException(
                    $"Discord role '{role.Name}' does not exist. Run setup first.")),
            StringComparer.OrdinalIgnoreCase);
        var roleTargets = BuildRoleTargets(guild, configuration, configuredRoles);

        foreach (var configuredCategory in configuration.Categories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var category = guild.CategoryChannels.FirstOrDefault(existing =>
                MatchesConfiguredCategoryName(existing.Name, configuredCategory));

            if (category is null)
            {
                logger.LogWarning(
                    "Skipping missing category {CategoryName} during permissions-only update.",
                    configuredCategory.Name);
                continue;
            }

            var categoryOverwrites = BuildOverwrites(
                configuration.PermissionProfiles[configuredCategory.PermissionProfile],
                roleTargets);

            if (dryRun)
            {
                logger.LogInformation(
                    "Would update permissions for category {CategoryName}.",
                    configuredCategory.Name);
            }
            else
            {
                logger.LogInformation(
                    "Updating permissions for category {CategoryName}.",
                    configuredCategory.Name);
                await category.ModifyAsync(
                    properties => properties.PermissionOverwrites = categoryOverwrites,
                    SetupRequest);
            }

            foreach (var configuredChannel in configuredCategory.Channels)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var channel = FindExistingChannel(
                    guild,
                    configuredChannel,
                    category.Id,
                    []);

                if (channel is null)
                {
                    logger.LogWarning(
                        "Skipping missing channel {CategoryName}/{ChannelName} during " +
                        "permissions-only update.",
                        configuredCategory.Name,
                        configuredChannel.Name);
                    continue;
                }

                var permissionProfile = configuredChannel.PermissionProfile
                    ?? configuredCategory.PermissionProfile;
                var channelOverwrites = BuildOverwrites(
                    configuration.PermissionProfiles[permissionProfile],
                    roleTargets);

                if (dryRun)
                {
                    logger.LogInformation(
                        "Would update permissions for channel {CategoryName}/{ChannelName}.",
                        configuredCategory.Name,
                        configuredChannel.Name);
                }
                else
                {
                    logger.LogInformation(
                        "Updating permissions for channel {CategoryName}/{ChannelName}.",
                        configuredCategory.Name,
                        configuredChannel.Name);
                    await channel.ModifyAsync(
                        properties => properties.PermissionOverwrites = channelOverwrites,
                        SetupRequest);
                }
            }
        }
    }

    public async Task ApplyForumTagsOnlyAsync(
        SocketGuild guild,
        DiscordServerSetupConfigDTO configuration,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        foreach (var configuredCategory in configuration.Categories)
        {
            var category = guild.CategoryChannels.FirstOrDefault(existing =>
                MatchesConfiguredCategoryName(existing.Name, configuredCategory));

            if (category is null)
                continue;

            foreach (var configuredChannel in configuredCategory.Channels.Where(channel =>
                         channel.Type.Equals("FORUM", StringComparison.OrdinalIgnoreCase)))
            {
                cancellationToken.ThrowIfCancellationRequested();

                var forum = FindExistingChannel(
                    guild,
                    configuredChannel,
                    category.Id,
                    []) as IForumChannel;

                if (forum is null)
                {
                    logger.LogWarning(
                        "Skipping missing forum {CategoryName}/{ChannelName} during " +
                        "forum-tags-only update.",
                        configuredCategory.Name,
                        configuredChannel.Name);
                    continue;
                }

                var tags = configuredChannel.AvailableTags.Select(tagName =>
                {
                    var existingTag = forum.Tags.FirstOrDefault(tag =>
                        string.Equals(tag.Name, tagName, StringComparison.OrdinalIgnoreCase));

                    return new ForumTagBuilder(
                        tagName,
                        existingTag.Id == 0 ? null : existingTag.Id,
                        false).Build();
                }).ToArray();

                if (dryRun)
                {
                    logger.LogInformation(
                        "Would update {TagCount} tags for forum {CategoryName}/{ChannelName}.",
                        tags.Length,
                        configuredCategory.Name,
                        configuredChannel.Name);
                    continue;
                }

                logger.LogInformation(
                    "Updating {TagCount} tags for forum {CategoryName}/{ChannelName}.",
                    tags.Length,
                    configuredCategory.Name,
                    configuredChannel.Name);

                await forum.ModifyAsync(
                    properties => properties.Tags = tags,
                    SetupRequest);
            }
        }
    }

    private async Task<Dictionary<string, ICategoryChannel>> EnsureCategoriesAsync(
        SocketGuild guild,
        DiscordServerSetupConfigDTO configuration,
        IReadOnlyDictionary<string, ulong> roleTargets)
    {
        var result = new Dictionary<string, ICategoryChannel>(StringComparer.OrdinalIgnoreCase);

        foreach (var configuredCategory in configuration.Categories)
        {
            var overwrites = BuildOverwrites(
                configuration.PermissionProfiles[configuredCategory.PermissionProfile],
                roleTargets);

            var existing = guild.CategoryChannels.FirstOrDefault(category =>
                MatchesConfiguredCategoryName(category.Name, configuredCategory));

            if (existing is null)
            {
                logger.LogInformation("Creating category {CategoryName}.", configuredCategory.Name);

                var created = await guild.CreateCategoryChannelAsync(
                    configuredCategory.Name,
                    properties => properties.PermissionOverwrites = overwrites,
                    SetupRequest);

                result[configuredCategory.Key] = created;
                continue;
            }

            logger.LogInformation("Updating category {CategoryName}.", configuredCategory.Name);

            await existing.ModifyAsync(properties =>
            {
                properties.Name = configuredCategory.Name;
                properties.PermissionOverwrites = overwrites;
            }, SetupRequest);

            result[configuredCategory.Key] = existing;
        }

        return result;
    }

    private async Task<Dictionary<string, IGuildChannel>> EnsureChannelsAsync(
        SocketGuild guild,
        DiscordServerSetupConfigDTO configuration,
        IReadOnlyDictionary<string, ICategoryChannel> categories,
        IReadOnlyDictionary<string, ulong> roleTargets,
        bool includeCommunityChannels,
        Dictionary<string, IGuildChannel>? existingResult = null)
    {
        var result = existingResult ?? new Dictionary<string, IGuildChannel>(StringComparer.OrdinalIgnoreCase);
        var communityAvailable = includeCommunityChannels ||
            (guild.Features.Value & GuildFeature.Community) == GuildFeature.Community;

        foreach (var configuredCategory in configuration.Categories)
        {
            var category = categories[configuredCategory.Key];

            foreach (var configuredChannel in configuredCategory.Channels)
            {
                var reference = $"{configuredCategory.Key}.{configuredChannel.Key}";
                var permissionProfile = configuredChannel.PermissionProfile
                    ?? configuredCategory.PermissionProfile;
                var overwrites = BuildOverwrites(
                    configuration.PermissionProfiles[permissionProfile],
                    roleTargets);

                if (configuredChannel.Type.Equals("FORUM", StringComparison.OrdinalIgnoreCase) &&
                    !includeCommunityChannels)
                {
                    continue;
                }

                var channel = FindExistingChannel(
                    guild,
                    configuredChannel,
                    category.Id,
                    result.Values);

                channel = configuredChannel.Type.ToUpperInvariant() switch
                {
                    "TEXT" => await EnsureTextChannelAsync(
                        guild, channel, configuredChannel, category.Id, overwrites, announcement: false),
                    "ANNOUNCEMENT" => await EnsureTextChannelAsync(
                        guild, channel, configuredChannel, category.Id, overwrites,
                        announcement: communityAvailable),
                    "VOICE" => await EnsureVoiceChannelAsync(
                        guild, channel, configuredChannel, category.Id, overwrites),
                    "FORUM" => await EnsureForumChannelAsync(
                        guild, channel, configuredChannel, category.Id, overwrites),
                    _ => throw new InvalidOperationException(
                        $"Unsupported Discord channel type: {configuredChannel.Type}.")
                };

                result[reference] = channel;
            }
        }

        return result;
    }

    private async Task<IGuildChannel> EnsureTextChannelAsync(
        SocketGuild guild,
        IGuildChannel? existing,
        DiscordChannelConfigDTO configured,
        ulong categoryId,
        IReadOnlyCollection<Overwrite> overwrites,
        bool announcement)
    {
        if (existing is not ITextChannel textChannel)
        {
            logger.LogInformation(
                "Creating {Type} channel {ChannelName}.",
                announcement ? "announcement" : "text",
                configured.Name);

            return await guild.CreateTextChannelAsync(
                configured.Name,
                properties =>
                {
                    properties.CategoryId = categoryId;
                    properties.Topic = configured.Topic;
                    properties.SlowModeInterval = configured.SlowmodeSeconds ?? 0;
                    properties.DefaultSlowModeInterval = configured.DefaultThreadSlowmodeSeconds ?? 0;
                    properties.PermissionOverwrites =
                        new Optional<IEnumerable<Overwrite>>(overwrites);
                    properties.ChannelType = announcement ? ChannelType.News : ChannelType.Text;
                },
                SetupRequest);
        }

        logger.LogInformation("Updating text channel {ChannelName}.", configured.Name);

        try
        {
            await ModifyTextChannelAsync(includeTopic: true);
        }
        catch (HttpException exception)
            when (exception.Message.Contains("CHANNEL_TOPIC_INVALID", StringComparison.Ordinal))
        {
            logger.LogWarning(
                "Discord rejected the configured topic for {ChannelName}; preserving its current topic.",
                configured.Name);

            await ModifyTextChannelAsync(includeTopic: false);
        }

        return textChannel;

        Task ModifyTextChannelAsync(bool includeTopic) =>
            textChannel.ModifyAsync(properties =>
            {
                properties.Name = configured.Name;
                properties.CategoryId = categoryId;
                if (includeTopic)
                    properties.Topic = configured.Topic;
                properties.SlowModeInterval = configured.SlowmodeSeconds ?? 0;
                properties.DefaultSlowModeInterval = configured.DefaultThreadSlowmodeSeconds ?? 0;
                properties.PermissionOverwrites =
                    new Optional<IEnumerable<Overwrite>>(overwrites);
            }, SetupRequest);
    }

    private async Task<IGuildChannel> EnsureVoiceChannelAsync(
        SocketGuild guild,
        IGuildChannel? existing,
        DiscordChannelConfigDTO configured,
        ulong categoryId,
        IReadOnlyCollection<Overwrite> overwrites)
    {
        if (existing is not IVoiceChannel voiceChannel)
        {
            logger.LogInformation("Creating voice channel {ChannelName}.", configured.Name);

            return await guild.CreateVoiceChannelAsync(
                configured.Name,
                properties =>
                {
                    properties.CategoryId = categoryId;
                    properties.Bitrate = configured.Bitrate ?? 64_000;
                    properties.UserLimit = configured.UserLimit;
                    properties.PermissionOverwrites =
                        new Optional<IEnumerable<Overwrite>>(overwrites);
                },
                SetupRequest);
        }

        logger.LogInformation("Updating voice channel {ChannelName}.", configured.Name);

        await voiceChannel.ModifyAsync((VoiceChannelProperties properties) =>
        {
            properties.Name = configured.Name;
            properties.CategoryId = categoryId;
            properties.Bitrate = configured.Bitrate ?? 64_000;
            properties.UserLimit = configured.UserLimit;
            properties.PermissionOverwrites =
                new Optional<IEnumerable<Overwrite>>(overwrites);
        }, SetupRequest);

        return voiceChannel;
    }

    private async Task<IGuildChannel> EnsureForumChannelAsync(
        SocketGuild guild,
        IGuildChannel? existing,
        DiscordChannelConfigDTO configured,
        ulong categoryId,
        IReadOnlyCollection<Overwrite> overwrites)
    {
        if (existing is not IForumChannel forumChannel)
        {
            logger.LogInformation("Creating forum channel {ChannelName}.", configured.Name);

            var created = await guild.CreateForumChannelAsync(
                configured.Name,
                properties => ApplyForumProperties(
                    properties,
                    configured,
                    categoryId,
                    overwrites,
                    [],
                    includeTags: false),
                SetupRequest);

            if (configured.AvailableTags.Length > 0)
            {
                await created.ModifyAsync(properties => ApplyForumProperties(
                    properties,
                    configured,
                    categoryId,
                    overwrites,
                    [],
                    includeTags: true),
                    SetupRequest);
            }

            return created;
        }

        logger.LogInformation("Updating forum channel {ChannelName}.", configured.Name);

        await forumChannel.ModifyAsync(properties => ApplyForumProperties(
            properties,
            configured,
            categoryId,
            overwrites,
            forumChannel.Tags,
            includeTags: true),
            SetupRequest);

        return forumChannel;
    }

    private static void ApplyForumProperties(
        ForumChannelProperties properties,
        DiscordChannelConfigDTO configured,
        ulong categoryId,
        IReadOnlyCollection<Overwrite> overwrites,
        IReadOnlyCollection<ForumTag> existingTags,
        bool includeTags)
    {
        properties.Name = configured.Name;
        properties.CategoryId = categoryId;
        properties.Topic = configured.Topic;
        properties.DefaultSlowModeInterval = configured.DefaultThreadSlowmodeSeconds ?? 0;
        properties.PermissionOverwrites =
            new Optional<IEnumerable<Overwrite>>(overwrites);
        properties.DefaultReactionEmoji = string.IsNullOrWhiteSpace(configured.DefaultReactionEmoji)
            ? null
            : new Emoji(configured.DefaultReactionEmoji);
        properties.DefaultSortOrder = ParseSortOrder(configured.DefaultSortOrder);
        properties.DefaultLayout = ParseForumLayout(configured.DefaultForumLayout);
        if (!includeTags)
            return;

        properties.Tags = configured.AvailableTags.Select(tagName =>
        {
            var existingTag = existingTags.FirstOrDefault(tag =>
                string.Equals(tag.Name, tagName, StringComparison.OrdinalIgnoreCase));

            return new ForumTagBuilder(tagName, existingTag.Id == 0 ? null : existingTag.Id, false).Build();
        }).ToArray();
    }

    private async Task EnsureInitialMessagesAsync(
        DiscordServerSetupConfigDTO configuration,
        IReadOnlyDictionary<string, IGuildChannel> channels,
        ulong botUserId)
    {
        foreach (var category in configuration.Categories)
        {
            foreach (var configuredChannel in category.Channels.Where(channel => channel.InitialMessages.Length > 0))
            {
                var reference = $"{category.Key}.{configuredChannel.Key}";

                if (!channels.TryGetValue(reference, out var guildChannel) ||
                    guildChannel is not IMessageChannel messageChannel)
                {
                    throw new InvalidOperationException(
                        $"Initial messages require a text channel: {reference}.");
                }

                var recentMessages = await messageChannel
                    .GetMessagesAsync(100, CacheMode.AllowDownload, SetupRequest)
                    .FlattenAsync();

                foreach (var initialMessage in configuredChannel.InitialMessages)
                {
                    var existing = recentMessages
                        .OfType<IUserMessage>()
                        .FirstOrDefault(message => message.Content == initialMessage.Content);

                    if (existing is null && configuredChannel.InitialMessages.Length == 1)
                    {
                        existing = recentMessages
                            .OfType<IUserMessage>()
                            .FirstOrDefault(message => message.Author.Id == botUserId && message.IsPinned);

                        if (existing is not null)
                        {
                            logger.LogInformation(
                                "Updating pinned setup message in {ChannelName}.",
                                configuredChannel.Name);
                            await existing.ModifyAsync(
                                properties => properties.Content = initialMessage.Content,
                                SetupRequest);
                        }
                    }

                    if (existing is null)
                    {
                        logger.LogInformation("Posting initial message in {ChannelName}.", configuredChannel.Name);
                        existing = await messageChannel.SendMessageAsync(
                            initialMessage.Content,
                            options: SetupRequest);
                    }

                    if (initialMessage.Pin && !existing.IsPinned)
                        await existing.PinAsync(SetupRequest);
                }
            }
        }
    }

    private static IGuildChannel? FindExistingChannel(
        SocketGuild guild,
        DiscordChannelConfigDTO configured,
        ulong categoryId,
        IEnumerable<IGuildChannel> newlyResolved)
    {
        var candidates = guild.Channels
            .Cast<IGuildChannel>()
            .Concat(newlyResolved)
            .Where(channel =>
                MatchesConfiguredName(channel.Name, configured) &&
                IsCompatibleType(channel, configured.Type))
            .DistinctBy(channel => channel.Id)
            .ToArray();

        return candidates.FirstOrDefault(channel => GetCategoryId(channel) == categoryId)
            ?? candidates.FirstOrDefault(channel => GetCategoryId(channel) is null)
            ?? candidates.FirstOrDefault();
    }

    private static ulong? GetCategoryId(IGuildChannel channel) =>
        (channel as INestedChannel)?.CategoryId;

    private static bool IsCompatibleType(IGuildChannel channel, string configuredType) =>
        configuredType.ToUpperInvariant() switch
        {
            "TEXT" => channel.ChannelType == ChannelType.Text,
            "ANNOUNCEMENT" => channel.ChannelType is ChannelType.News or ChannelType.Text,
            "VOICE" => channel.ChannelType == ChannelType.Voice,
            "FORUM" => channel.ChannelType == ChannelType.Forum,
            _ => false
        };

    private static Dictionary<string, IGuildChannel> BuildExistingChannelMap(
        SocketGuild guild,
        DiscordServerSetupConfigDTO configuration)
    {
        var result = new Dictionary<string, IGuildChannel>(StringComparer.OrdinalIgnoreCase);

        foreach (var category in configuration.Categories)
        {
            foreach (var channel in category.Channels)
            {
                var existing = guild.Channels.FirstOrDefault(candidate =>
                    MatchesConfiguredName(candidate.Name, channel) &&
                    IsCompatibleType(candidate, channel.Type));

                if (existing is not null)
                    result[$"{category.Key}.{channel.Key}"] = existing;
            }
        }

        return result;
    }

    private void LogPlan(SocketGuild guild, DiscordServerSetupConfigDTO configuration)
    {
        foreach (var category in configuration.Categories)
        {
            var existingCategory = guild.CategoryChannels.FirstOrDefault(candidate =>
                MatchesConfiguredCategoryName(candidate.Name, category));

            logger.LogInformation(
                "{Mode} category {CategoryName} with profile {PermissionProfile}.",
                existingCategory is null ? "Would create" : "Would update",
                category.Name,
                category.PermissionProfile);

            foreach (var channel in category.Channels)
            {
                var existing = guild.Channels.FirstOrDefault(candidate =>
                    MatchesConfiguredName(candidate.Name, channel) &&
                    IsCompatibleType(candidate, channel.Type));

                logger.LogInformation(
                    "{Mode} {ChannelType} channel {CategoryName}/{ChannelName}.",
                    existing is null ? "Would create" : "Would update",
                    channel.Type,
                    category.Name,
                    channel.Name);
            }
        }
    }

    private void LogReconciliationPlan(
        SocketGuild guild,
        DiscordServerSetupConfigDTO configuration)
    {
        var knownCategoryIds = configuration.Categories
            .Select(configured => guild.CategoryChannels.FirstOrDefault(category =>
                MatchesConfiguredCategoryName(category.Name, configured)))
            .Where(category => category is not null)
            .Select(category => category!.Id)
            .ToHashSet();

        var knownChannelIds = BuildExistingChannelMap(guild, configuration)
            .Values
            .Select(channel => channel.Id)
            .ToHashSet();

        if (configuration.Reconciliation.DeleteUnknownChannels)
        {
            foreach (var channel in guild.Channels.Where(channel =>
                channel is not ICategoryChannel &&
                !knownChannelIds.Contains(channel.Id)))
            {
                logger.LogWarning(
                    "Would delete unmanaged channel {ChannelName} ({ChannelId}).",
                    channel.Name,
                    channel.Id);
            }
        }

        if (configuration.Reconciliation.DeleteUnknownCategories)
        {
            foreach (var category in guild.CategoryChannels.Where(category =>
                !knownCategoryIds.Contains(category.Id)))
            {
                logger.LogWarning(
                    "Would delete unmanaged category {CategoryName} ({CategoryId}).",
                    category.Name,
                    category.Id);
            }
        }
    }

    private async Task ReconcileUnknownChannelsAsync(
        SocketGuild guild,
        DiscordServerSetupConfigDTO configuration,
        IReadOnlyDictionary<string, ICategoryChannel> categories,
        IReadOnlyDictionary<string, IGuildChannel> channels)
    {
        var knownChannelIds = channels.Values.Select(channel => channel.Id).ToHashSet();
        var knownCategoryIds = categories.Values.Select(category => category.Id).ToHashSet();

        if (configuration.Reconciliation.DeleteUnknownChannels)
        {
            var staleChannels = guild.Channels
                .Where(channel =>
                    channel is not ICategoryChannel &&
                    !knownChannelIds.Contains(channel.Id))
                .ToArray();

            foreach (var channel in staleChannels)
            {
                logger.LogWarning(
                    "Deleting unmanaged channel {ChannelName} ({ChannelId}).",
                    channel.Name,
                    channel.Id);

                await channel.DeleteAsync(SetupRequest);
            }
        }

        if (configuration.Reconciliation.DeleteUnknownCategories)
        {
            var staleCategories = guild.CategoryChannels
                .Where(category => !knownCategoryIds.Contains(category.Id))
                .ToArray();

            foreach (var category in staleCategories)
            {
                logger.LogWarning(
                    "Deleting unmanaged category {CategoryName} ({CategoryId}).",
                    category.Name,
                    category.Id);

                await category.DeleteAsync(SetupRequest);
            }
        }
    }

    private static bool MatchesConfiguredName(
        string actualName,
        DiscordChannelConfigDTO configured) =>
        string.Equals(actualName, configured.Name, StringComparison.OrdinalIgnoreCase) ||
        configured.PreviousNames.Any(previousName =>
            string.Equals(actualName, previousName, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesConfiguredCategoryName(
        string actualName,
        DiscordCategoryConfigDTO configured) =>
        string.Equals(actualName, configured.Name, StringComparison.OrdinalIgnoreCase) ||
        configured.PreviousNames.Any(previousName =>
            string.Equals(actualName, previousName, StringComparison.OrdinalIgnoreCase));

    private static Dictionary<string, ulong> BuildRoleTargets(
        SocketGuild guild,
        DiscordServerSetupConfigDTO configuration,
        IReadOnlyDictionary<string, IRole> configuredRoles)
    {
        var result = configuredRoles.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Id,
            StringComparer.OrdinalIgnoreCase);

        result["everyone"] = guild.EveryoneRole.Id;

        var botRole = guild.CurrentUser.Roles.FirstOrDefault(role =>
                string.Equals(role.Name, configuration.Bot.RoleName, StringComparison.OrdinalIgnoreCase))
            ?? guild.CurrentUser.Roles
                .Where(role => role.IsManaged)
                .OrderByDescending(role => role.Position)
                .FirstOrDefault();

        if (botRole is null)
            throw new InvalidOperationException("The bot's managed Discord role could not be resolved.");

        result["bot"] = botRole.Id;
        return result;
    }

    private static Overwrite[] BuildOverwrites(
        IEnumerable<DiscordPermissionOverwriteConfigDTO> configuredOverwrites,
        IReadOnlyDictionary<string, ulong> roleTargets)
    {
        return configuredOverwrites.Select(configured =>
        {
            if (!roleTargets.TryGetValue(configured.Target, out var roleId))
                throw new InvalidOperationException($"Unknown permission target: {configured.Target}.");

            var allow = BuildPermissionBits(configured.Allow);
            var deny = BuildPermissionBits(configured.Deny);

            if ((allow & deny) != 0)
                throw new InvalidOperationException(
                    $"Permission profile both allows and denies a permission for {configured.Target}.");

            return new Overwrite(
                roleId,
                PermissionTarget.Role,
                new OverwritePermissions(allow, deny));
        }).ToArray();
    }

    private static void ValidatePermissionProfiles(DiscordServerSetupConfigDTO configuration)
    {
        var validTargets = configuration.Roles
            .Select(role => role.Key)
            .Append("everyone")
            .Append("bot")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var (profileName, overwrites) in configuration.PermissionProfiles)
        {
            foreach (var overwrite in overwrites)
            {
                if (!validTargets.Contains(overwrite.Target))
                    throw new InvalidOperationException(
                        $"Permission profile {profileName} has unknown target {overwrite.Target}.");

                _ = BuildPermissionBits(overwrite.Allow);
                _ = BuildPermissionBits(overwrite.Deny);
            }
        }

        foreach (var category in configuration.Categories)
        {
            if (!configuration.PermissionProfiles.ContainsKey(category.PermissionProfile))
            {
                throw new InvalidOperationException(
                    $"Category '{category.Key}' references unknown permission profile " +
                    $"'{category.PermissionProfile}'.");
            }

            foreach (var channel in category.Channels.Where(channel =>
                         !string.IsNullOrWhiteSpace(channel.PermissionProfile)))
            {
                if (!configuration.PermissionProfiles.ContainsKey(channel.PermissionProfile!))
                {
                    throw new InvalidOperationException(
                        $"Channel '{category.Key}.{channel.Key}' references unknown permission " +
                        $"profile '{channel.PermissionProfile}'.");
                }
            }
        }
    }

    private static ulong BuildPermissionBits(IEnumerable<string> names)
    {
        ulong value = 0;

        foreach (var configuredName in names)
        {
            var enumName = configuredName switch
            {
                "UseVoiceActivity" => nameof(ChannelPermission.UseVAD),
                _ => configuredName
            };

            if (!Enum.TryParse<ChannelPermission>(enumName, true, out var permission))
                throw new InvalidOperationException($"Unknown channel permission: {configuredName}.");

            value |= (ulong)permission;
        }

        return value;
    }

    private static ForumSortOrder ParseSortOrder(string? value) =>
        value?.ToUpperInvariant() switch
        {
            null or "LATEST_ACTIVITY" => ForumSortOrder.LatestActivity,
            "CREATION_DATE" => ForumSortOrder.CreationDate,
            _ => throw new InvalidOperationException($"Unknown forum sort order: {value}.")
        };

    private static ForumLayout ParseForumLayout(string? value) =>
        value?.ToUpperInvariant() switch
        {
            null or "DEFAULT" => ForumLayout.Default,
            "LIST" => ForumLayout.List,
            "GALLERY" or "GRID" => ForumLayout.Grid,
            _ => throw new InvalidOperationException($"Unknown forum layout: {value}.")
        };
}
