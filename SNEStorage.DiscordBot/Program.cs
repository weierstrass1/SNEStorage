using Discord;
using Discord.WebSocket;
using Microsoft.Extensions.Options;
using SNEStorage.DiscordBot.DTOs;
using SNEStorage.DiscordBot.Services;
using System.Security.Cryptography;
using System.Text;

var permissionsOnly = args.Contains(
    "--permissions-only",
    StringComparer.OrdinalIgnoreCase);

var forumTagsOnly = args.Contains(
    "--forum-tags-only",
    StringComparer.OrdinalIgnoreCase);

var setupMode = permissionsOnly || forumTagsOnly || args.Contains(
    "--setup",
    StringComparer.OrdinalIgnoreCase);

var dryRun = args.Contains(
    "--dry-run",
    StringComparer.OrdinalIgnoreCase);

var hostArguments = args
    .Where(argument =>
        !argument.Equals("--setup", StringComparison.OrdinalIgnoreCase) &&
        !argument.Equals("--permissions-only", StringComparison.OrdinalIgnoreCase) &&
        !argument.Equals("--forum-tags-only", StringComparison.OrdinalIgnoreCase) &&
        !argument.Equals("--dry-run", StringComparison.OrdinalIgnoreCase))
    .ToArray();

var builder = WebApplication.CreateBuilder(hostArguments);

builder.Services.Configure<DiscordDTO>(
    builder.Configuration.GetSection("Discord"));
builder.Services.Configure<SNEStorageApiDTO>(
    builder.Configuration.GetSection("SNEStorage"));

builder.Services.AddSingleton(new DiscordSocketClient(
    new DiscordSocketConfig
    {
        GatewayIntents =
            GatewayIntents.Guilds |
            GatewayIntents.GuildMembers,

        AlwaysDownloadUsers = false,
        LogGatewayIntentWarnings = true
    }));

builder.Services.AddSingleton(new HttpClient
{
    Timeout = TimeSpan.FromSeconds(15)
});
builder.Services.AddSingleton(new BotRunMode(
    setupMode,
    dryRun,
    permissionsOnly,
    forumTagsOnly));
builder.Services.AddSingleton<SNEStorageApiClient>();
builder.Services.AddSingleton<DiscordMemberSynchronizer>();
builder.Services.AddSingleton<DiscordAccessService>();
builder.Services.AddSingleton<DiscordBotEventDispatcher>();
builder.Services.AddSingleton<DiscordResourceNotificationService>();
builder.Services.AddSingleton<DiscordUserNotificationService>();
builder.Services.AddSingleton<DiscordNewsNotificationService>();
builder.Services.AddSingleton<DiscordRoleProvisioner>();
builder.Services.AddSingleton<DiscordGuildProvisioner>();
builder.Services.AddSingleton<DiscordChannelProvisioner>();
builder.Services.AddSingleton<DiscordAutoModProvisioner>();
builder.Services.AddSingleton<DiscordServerConfigLoader>();
if (setupMode)
    builder.Services.AddHostedService<DiscordSetupWorker>();
else
    builder.Services.AddHostedService<Worker>();


var app = builder.Build();

app.MapGet("/", () => Results.Ok(new
{
    service = "SNEStorage Discord Bot",
    status = "running"
}));

app.MapGet("/health", (DiscordSocketClient discordClient) =>
{
    var connected = discordClient.ConnectionState == ConnectionState.Connected;

    return Results.Json(
        new
        {
            service = "SNEStorage Discord Bot",
            status = connected ? "healthy" : "starting",
            discord = discordClient.ConnectionState.ToString(),
            accessButton = DiscordAccessService.AllowAccessButtonId
        },
        statusCode: connected
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable);
});

app.MapPost(
    "/api/internal/discord/events",
    async (
        DiscordBotEventDTO notification,
        HttpRequest request,
        IOptions<SNEStorageApiDTO> apiOptions,
        DiscordBotEventDispatcher dispatcher,
        CancellationToken cancellationToken) =>
    {
        if (!HasValidBotKey(request, apiOptions.Value.BotApiKey))
            return Results.Unauthorized();

        var result = await dispatcher.DispatchAsync(
            notification,
            cancellationToken);

        var response = new
        {
            notification.EventId,
            status = result.Status.ToString(),
            changes = result.Changes ?? []
        };

        return result.Status switch
        {
            DiscordBotEventDispatchStatus.Success => Results.Ok(response),
            DiscordBotEventDispatchStatus.InvalidEvent =>
                Results.BadRequest(response),
            DiscordBotEventDispatchStatus.EntityNotFound =>
                Results.NotFound(response),
            DiscordBotEventDispatchStatus.Ineligible => Results.Json(
                response,
                statusCode: StatusCodes.Status403Forbidden),
            DiscordBotEventDispatchStatus.UnsupportedEvent => Results.Json(
                response,
                statusCode: StatusCodes.Status501NotImplemented),
            _ => Results.Json(
                response,
                statusCode: StatusCodes.Status503ServiceUnavailable)
        };
    });

app.Logger.LogInformation(
    "SNEStorage Discord Bot web host configured. Health endpoint: /health");

await app.RunAsync();

static bool HasValidBotKey(HttpRequest request, string expectedKey)
{
    const string botKeyHeader = "X-SNEStorage-Bot-Key";
    string suppliedKey = request.Headers[botKeyHeader].ToString();

    if (string.IsNullOrWhiteSpace(suppliedKey) ||
        string.IsNullOrWhiteSpace(expectedKey))
    {
        return false;
    }

    byte[] suppliedHash = SHA256.HashData(
        Encoding.UTF8.GetBytes(suppliedKey));
    byte[] expectedHash = SHA256.HashData(
        Encoding.UTF8.GetBytes(expectedKey));

    return CryptographicOperations.FixedTimeEquals(
        suppliedHash,
        expectedHash);
}
