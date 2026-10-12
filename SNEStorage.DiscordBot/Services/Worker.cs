using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using SNEStorage.DiscordBot.DTOs;

namespace SNEStorage.DiscordBot.Services;

public sealed class Worker(
    DiscordSocketClient client,
    IOptions<DiscordDTO> options,
    DiscordServerConfigLoader configLoader,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly DiscordSocketClient _client = client;
    private readonly DiscordDTO _options = options.Value;
    private readonly ILogger<Worker> _logger = logger;
    private readonly DiscordServerConfigLoader _configLoader = configLoader;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ValidateConfiguration();

        var serverConfig = await _configLoader.LoadSummaryAsync(stoppingToken);

        _logger.LogInformation(
            "Loaded Discord configuration v{SchemaVersion}: " +
            "{ServerName}, {Roles} roles, {Categories} categories, " +
            "{Channels} channels.",
            serverConfig.SchemaVersion,
            serverConfig.ServerName,
            serverConfig.RoleCount,
            serverConfig.CategoryCount,
            serverConfig.ChannelCount);

        _client.Log += HandleDiscordLogAsync;
        _client.Ready += HandleReadyAsync;

        _logger.LogInformation("Connecting SNEStorage Bot to Discord...");

        await _client.LoginAsync(
            TokenType.Bot,
            _options.BotToken);

        await _client.StartAsync();

        try
        {
            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            _logger.LogInformation("Disconnecting SNEStorage Bot...");

            await _client.StopAsync();
            await _client.LogoutAsync();
        }
    }

    private Task HandleReadyAsync()
    {
        _logger.LogInformation(
            "Discord connected as {Username} ({UserId}).",
            _client.CurrentUser.Username,
            _client.CurrentUser.Id);

        if (!ulong.TryParse(_options.GuildId, out var guildId))
        {
            _logger.LogError("Discord GuildId is invalid.");
            return Task.CompletedTask;
        }

        var guild = _client.GetGuild(guildId);

        if (guild is null)
        {
            _logger.LogError(
                "The bot cannot find the configured server {GuildId}.",
                guildId);

            return Task.CompletedTask;
        }

        _logger.LogInformation(
            "SNEStorage Bot is connected to {GuildName} ({GuildId}).",
            guild.Name,
            guild.Id);

        return Task.CompletedTask;
    }

    private Task HandleDiscordLogAsync(LogMessage message)
    {
        var level = message.Severity switch
        {
            LogSeverity.Critical => LogLevel.Critical,
            LogSeverity.Error => LogLevel.Error,
            LogSeverity.Warning => LogLevel.Warning,
            LogSeverity.Info => LogLevel.Information,
            LogSeverity.Verbose => LogLevel.Trace,
            LogSeverity.Debug => LogLevel.Debug,
            _ => LogLevel.Information
        };

        _logger.Log(
            level,
            message.Exception,
            "[Discord] {Message}",
            message.Message);

        return Task.CompletedTask;
    }
    private void ValidateConfiguration()
    {
        if (string.IsNullOrWhiteSpace(_options.BotToken))
            throw new InvalidOperationException(
                "Discord:BotToken is missing.");

        if (string.IsNullOrWhiteSpace(_options.ApplicationId))
            throw new InvalidOperationException(
                "Discord:ApplicationId is missing.");

        if (!ulong.TryParse(_options.GuildId, out _))
            throw new InvalidOperationException(
                "Discord:GuildId is missing or invalid.");
    }
}
