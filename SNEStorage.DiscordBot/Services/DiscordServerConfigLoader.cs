using System.Text.Json;
using Microsoft.Extensions.Options;
using SNEStorage.DiscordBot.DTOs;

namespace SNEStorage.DiscordBot.Services;

public sealed record DiscordServerConfigSummary(
    int SchemaVersion,
    string ServerName,
    int RoleCount,
    int CategoryCount,
    int ChannelCount);

public sealed class DiscordServerConfigLoader(
    IOptions<DiscordDTO> options)
{
    private readonly DiscordDTO _options = options.Value;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public async Task<DiscordServerSetupConfigDTO> LoadAsync(
        CancellationToken cancellationToken)
    {
        var path = GetConfigPath();

        await using var stream = File.OpenRead(path);

        var configuration = await JsonSerializer.DeserializeAsync<DiscordServerSetupConfigDTO>(
            stream,
            JsonOptions,
            cancellationToken);

        if (configuration is null)
        {
            throw new InvalidOperationException(
                "The Discord server configuration is empty or invalid.");
        }

        Validate(configuration);
        return configuration;
    }

    public string ResolveConfigRelativePath(string relativePath)
    {
        var configDirectory = Path.GetDirectoryName(GetConfigPath())
            ?? AppContext.BaseDirectory;

        return Path.GetFullPath(Path.Combine(configDirectory, relativePath));
    }

    public async Task<DiscordServerConfigSummary> LoadSummaryAsync(
        CancellationToken cancellationToken)
    {
        var configuration = await LoadAsync(cancellationToken);

        return new DiscordServerConfigSummary(
            configuration.SchemaVersion,
            configuration.Server.Name,
            configuration.Roles.Length,
            configuration.Categories.Length,
            configuration.Categories.Sum(category => category.Channels.Length));
    }

    public async Task<IReadOnlyList<DiscordRoleConfigDTO>> LoadRolesAsync(
        CancellationToken cancellationToken)
    {
        return (await LoadAsync(cancellationToken)).Roles;
    }

    public async Task<IReadOnlyList<DiscordCategoryConfigDTO>>
        LoadCategoriesAsync(CancellationToken cancellationToken)
    {
        return (await LoadAsync(cancellationToken)).Categories;
    }

    private string GetConfigPath()
    {
        var path = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, _options.ServerConfigPath));

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Discord server configuration was not found.",
                path);
        }

        return path;
    }

    private static void Validate(DiscordServerSetupConfigDTO configuration)
    {
        if (configuration.SchemaVersion != 1)
        {
            throw new InvalidOperationException(
                $"Unsupported Discord configuration schema: {configuration.SchemaVersion}.");
        }

        if (string.IsNullOrWhiteSpace(configuration.Server.Name))
            throw new InvalidOperationException("The Discord server name is missing.");

        if (configuration.Roles.Length == 0)
            throw new InvalidOperationException("The Discord configuration contains no roles.");

        EnsureUniqueKeys(configuration.Roles.Select(role => role.Key), "role");
        EnsureUniqueKeys(configuration.Categories.Select(category => category.Key), "category");

        var supportedTypes = new HashSet<string>(
            ["TEXT", "ANNOUNCEMENT", "FORUM", "VOICE"],
            StringComparer.OrdinalIgnoreCase);

        foreach (var category in configuration.Categories)
        {
            if (string.IsNullOrWhiteSpace(category.Name) || category.Channels.Length == 0)
                throw new InvalidOperationException($"Category {category.Key} requires a name and channels.");

            if (!configuration.PermissionProfiles.ContainsKey(category.PermissionProfile))
                throw new InvalidOperationException(
                    $"Category {category.Key} references unknown permission profile {category.PermissionProfile}.");

            EnsureUniqueKeys(category.Channels.Select(channel => channel.Key), $"channel in {category.Key}");

            foreach (var channel in category.Channels)
            {
                if (string.IsNullOrWhiteSpace(channel.Name) || !supportedTypes.Contains(channel.Type))
                    throw new InvalidOperationException(
                        $"Channel {category.Key}.{channel.Key} has an invalid name or type {channel.Type}.");
            }
        }
    }

    private static void EnsureUniqueKeys(IEnumerable<string> keys, string kind)
    {
        if (keys.Any(string.IsNullOrWhiteSpace) ||
            keys.GroupBy(key => key, StringComparer.OrdinalIgnoreCase).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException($"Discord {kind} keys must be present and unique.");
        }
    }
}
