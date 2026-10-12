using Discord;
using Discord.WebSocket;
using SNEStorage.DiscordBot;
using SNEStorage.DiscordBot.DTOs;
using SNEStorage.DiscordBot.Services;

var setupMode = args.Contains(
    "--setup",
    StringComparer.OrdinalIgnoreCase);

var dryRun = args.Contains(
    "--dry-run",
    StringComparer.OrdinalIgnoreCase);

var hostArguments = args
    .Where(argument =>
        !argument.Equals("--setup", StringComparison.OrdinalIgnoreCase) &&
        !argument.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
    .ToArray();

var builder = Host.CreateApplicationBuilder(hostArguments);

builder.Services.Configure<DiscordDTO>(
    builder.Configuration.GetSection("Discord"));

builder.Services.AddSingleton(new DiscordSocketClient(
    new DiscordSocketConfig
    {
        GatewayIntents =
            GatewayIntents.Guilds |
            GatewayIntents.GuildMembers,

        AlwaysDownloadUsers = false,
        LogGatewayIntentWarnings = true
    }));

builder.Services.AddSingleton(new HttpClient());
builder.Services.AddSingleton(new BotRunMode(setupMode, dryRun));
builder.Services.AddSingleton<DiscordRoleProvisioner>();
builder.Services.AddSingleton<DiscordGuildProvisioner>();
builder.Services.AddSingleton<DiscordChannelProvisioner>();
builder.Services.AddSingleton<DiscordAutoModProvisioner>();
builder.Services.AddSingleton<DiscordServerConfigLoader>();
if (setupMode)
    builder.Services.AddHostedService<DiscordSetupWorker>();
else
    builder.Services.AddHostedService<Worker>();


var host = builder.Build();
await host.RunAsync();
