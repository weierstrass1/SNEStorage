using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using SNEStorage.DiscordBot.DTOs;

namespace SNEStorage.DiscordBot.Services;

public sealed class Worker(
    DiscordSocketClient client,
    IOptions<DiscordDTO> options,
    DiscordServerConfigLoader configLoader,
    DiscordAccessService accessService,
    ILogger<Worker> logger) : BackgroundService
{
    private readonly DiscordSocketClient _client = client;
    private readonly DiscordDTO _options = options.Value;
    private readonly ILogger<Worker> _logger = logger;
    private readonly DiscordServerConfigLoader _configLoader = configLoader;
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client.Log += HandleDiscordLogAsync;
        _client.Ready += HandleReadyAsync;
        _client.ButtonExecuted += accessService.HandleButtonAsync;
        _client.UserJoined += accessService.HandleUserJoinedAsync;

        try
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

            _logger.LogInformation("Connecting SNEStorage Bot to Discord...");

            await _client.LoginAsync(
                TokenType.Bot,
                _options.BotToken);

            await _client.StartAsync();

            await Task.Delay(
                Timeout.InfiniteTimeSpan,
                stoppingToken);
        }
        catch (OperationCanceledException)
            when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            _logger.LogCritical(
                exception,
                "SNEStorage Bot could not connect to Discord. " +
                "The web process will remain available for diagnostics.");

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
        }
        finally
        {
            _client.ButtonExecuted -= accessService.HandleButtonAsync;
            _client.UserJoined -= accessService.HandleUserJoinedAsync;

            _logger.LogInformation("Disconnecting SNEStorage Bot...");

            if (_client.ConnectionState != ConnectionState.Disconnected)
                await _client.StopAsync();

            if (_client.LoginState == LoginState.LoggedIn)
                await _client.LogoutAsync();
        }
    }

    private async Task HandleReadyAsync()
    {
        _logger.LogInformation(
            "Discord connected as {Username} ({UserId}).",
            _client.CurrentUser.Username,
            _client.CurrentUser.Id);

        if (!ulong.TryParse(_options.GuildId, out var guildId))
        {
            _logger.LogError("Discord GuildId is invalid.");
            return;
        }

        var guild = _client.GetGuild(guildId);

        if (guild is null)
        {
            _logger.LogError(
                "The bot cannot find the configured server {GuildId}.",
                guildId);

            return;
        }

        _logger.LogInformation(
            "SNEStorage Bot is connected to {GuildName} ({GuildId}).",
            guild.Name,
            guild.Id);

        try
        {
            await accessService.EnsureAccessMessageAsync(guild);
            _logger.LogInformation(
                "Discord access verification button is ready in the rules channel.");
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Could not create or update the Discord access verification message.");
        }
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
