using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using SNEStorage.DiscordBot.DTOs;
using SNEStorage.DiscordBot.Services;

namespace SNEStorage.DiscordBot;

public sealed class DiscordSetupWorker(
    DiscordSocketClient client,
    DiscordRoleProvisioner roleProvisioner,
    DiscordChannelProvisioner channelProvisioner,
    DiscordAutoModProvisioner autoModProvisioner,
    DiscordServerConfigLoader configLoader,
    BotRunMode runMode,
    IOptions<DiscordDTO> options,
    IHostApplicationLifetime applicationLifetime,
    ILogger<DiscordSetupWorker> logger) : BackgroundService
{
    private readonly TaskCompletionSource _ready =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _setupStarted;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ValidateConfiguration(options.Value);

        client.Log += message =>
        {
            logger.LogInformation(
                message.Exception,
                "[Discord] {Message}",
                message.Message);

            return Task.CompletedTask;
        };

        client.Ready += HandleReadyAsync;

        await client.LoginAsync(
            TokenType.Bot,
            options.Value.BotToken);

        await client.StartAsync();

        try
        {
            await _ready.Task.WaitAsync(stoppingToken);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException)
        {
            Environment.ExitCode = 1;

            logger.LogError(
                exception,
                "Discord server setup failed.");
        }
        finally
        {
            await client.StopAsync();
            await client.LogoutAsync();

            applicationLifetime.StopApplication();
        }
    }

    private async Task HandleReadyAsync()
    {
        if (Interlocked.Exchange(ref _setupStarted, 1) != 0)
            return;

        try
        {
            var guildId = ulong.Parse(options.Value.GuildId);
            var guild = client.GetGuild(guildId);

            if (guild is null)
            {
                throw new InvalidOperationException(
                    $"The bot cannot find guild {guildId}.");
            }

            logger.LogInformation(
                "Starting Discord server setup for {GuildName}. DryRun: {DryRun}.",
                guild.Name,
                runMode.DryRun);

            var configuration = await configLoader.LoadAsync(CancellationToken.None);

            var channelCount = configuration.Categories.Sum(
                category => category.Channels.Length);

            logger.LogInformation(
                "Configuration loaded: {CategoryCount} categories and {ChannelCount} channels.",
                configuration.Categories.Length,
                channelCount);

            if (runMode.PermissionsOnly)
            {
                await channelProvisioner.ApplyPermissionsOnlyAsync(
                    guild,
                    configuration,
                    runMode.DryRun,
                    CancellationToken.None);

                logger.LogInformation(
                    runMode.DryRun
                        ? "Discord permissions-only dry run completed."
                        : "Discord permissions-only update completed.");

                _ready.TrySetResult();
                return;
            }

            if (runMode.ForumTagsOnly)
            {
                await channelProvisioner.ApplyForumTagsOnlyAsync(
                    guild,
                    configuration,
                    runMode.DryRun,
                    CancellationToken.None);

                logger.LogInformation(
                    runMode.DryRun
                        ? "Discord forum-tags-only dry run completed."
                        : "Discord forum-tags-only update completed.");

                _ready.TrySetResult();
                return;
            }

            var roles = await roleProvisioner.ApplyAsync(
                guild,
                runMode.DryRun,
                CancellationToken.None);

            var channels = await channelProvisioner.ApplyAsync(
                guild,
                configuration,
                roles,
                runMode.DryRun,
                CancellationToken.None);

            await autoModProvisioner.ApplyAsync(
                guild,
                configuration,
                roles,
                channels,
                runMode.DryRun,
                CancellationToken.None);

            logger.LogInformation(
                runMode.DryRun
                    ? "Discord server dry run completed."
                    : "Discord server setup completed.");

            _ready.TrySetResult();
        }
        catch (Exception exception)
        {
            _ready.TrySetException(exception);
        }
    }

    private static void ValidateConfiguration(DiscordDTO configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.BotToken))
        {
            throw new InvalidOperationException(
                "Discord:BotToken is missing.");
        }

        if (!ulong.TryParse(configuration.GuildId, out _))
        {
            throw new InvalidOperationException(
                "Discord:GuildId is invalid.");
        }
    }
}
