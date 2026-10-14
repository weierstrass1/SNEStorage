using Discord;
using Discord.WebSocket;
using SNEStorage.DiscordBot.DTOs;
using System.Globalization;

namespace SNEStorage.DiscordBot.Services;

public sealed class DiscordRoleProvisioner(
    DiscordServerConfigLoader configLoader,
    ILogger<DiscordRoleProvisioner> logger)
{
    private readonly DiscordServerConfigLoader _configLoader = configLoader;
    private readonly ILogger<DiscordRoleProvisioner> _logger = logger;

    public async Task<IReadOnlyDictionary<string, IRole>> ApplyAsync(
        SocketGuild guild,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var configuredRoles = await _configLoader.LoadRolesAsync(
            cancellationToken);
        var resolvedRoles = new Dictionary<string, IRole>(
            StringComparer.OrdinalIgnoreCase);

        foreach (var configuredRole in configuredRoles)
        {
            var permissions = BuildPermissions(configuredRole);

            var colors = RoleColors.Solid(
                ParseColor(configuredRole.Color));

            var existingRole = guild.Roles.FirstOrDefault(role =>
                !role.IsManaged &&
                string.Equals(
                    role.Name,
                    configuredRole.Name,
                    StringComparison.OrdinalIgnoreCase));

            if (existingRole is null)
            {
                _logger.LogInformation(
                    "{Mode} role {RoleName}.",
                    dryRun ? "Would create" : "Creating",
                    configuredRole.Name);

                if (dryRun)
                    continue;

                var createdRole = await guild.CreateRoleAsync(
                    configuredRole.Name,
                    permissions,
                    colors,
                    configuredRole.Hoist,
                    configuredRole.Mentionable,
                    new RequestOptions
                    {
                        AuditLogReason =
                            "SNEStorage server configuration setup"
                    });

                resolvedRoles[configuredRole.Key] = createdRole;

                await EnsureOwnerAssignmentAsync(
                    guild,
                    createdRole,
                    configuredRole);

                continue;
            }

            resolvedRoles[configuredRole.Key] = existingRole;

            if (existingRole.IsManaged)
            {
                throw new InvalidOperationException(
                    $"Role {configuredRole.Name} is managed by Discord.");
            }

            var botUser = guild.CurrentUser;

            if (existingRole.Position >= botUser.Hierarchy)
            {
                if (dryRun || configuredRole.AssignToGuildOwner)
                {
                    _logger.LogWarning(
                        "Cannot update role {RoleName} because it is above the bot; preserving it unchanged.",
                        existingRole.Name);
                    continue;
                }

                throw new InvalidOperationException(
                    $"The bot role must be above {existingRole.Name}.");
            }

            _logger.LogInformation(
                "{Mode} role {RoleName}.",
                dryRun ? "Would update" : "Updating",
                configuredRole.Name);

            if (dryRun)
                continue;

            await existingRole.ModifyAsync(properties =>
            {
                properties.Name = configuredRole.Name;
                properties.Permissions = permissions;
                properties.Colors = colors;
                properties.Hoist = configuredRole.Hoist;
                properties.Mentionable = configuredRole.Mentionable;
            },
            new RequestOptions
            {
                AuditLogReason =
                    "SNEStorage server configuration setup"
            });

            await EnsureOwnerAssignmentAsync(
                guild,
                existingRole,
                configuredRole);
        }

        return resolvedRoles;
    }

    private async Task EnsureOwnerAssignmentAsync(
        SocketGuild guild,
        IRole role,
        DiscordRoleConfigDTO configuredRole)
    {
        if (!configuredRole.AssignToGuildOwner)
            return;

        var owner = guild.Owner ??
            await ((IGuild)guild).GetUserAsync(
                guild.OwnerId,
                CacheMode.AllowDownload,
                new RequestOptions
                {
                    AuditLogReason = "SNEStorage server configuration setup"
                }) ??
            throw new InvalidOperationException(
                $"The guild owner {guild.OwnerId} could not be resolved.");

        if (owner.RoleIds.Contains(role.Id))
            return;

        _logger.LogInformation(
            "Assigning role {RoleName} to guild owner {OwnerName}.",
            role.Name,
            owner.DisplayName);

        await owner.AddRoleAsync(
            role,
            new RequestOptions
            {
                AuditLogReason =
                    "SNEStorage administrator role assigned to guild owner"
            });
    }

    private static GuildPermissions BuildPermissions(
        DiscordRoleConfigDTO configuredRole)
    {
        ulong rawValue = 0;

        foreach (var configuredName in configuredRole.Permissions)
        {
            var enumName = configuredName switch
            {
                // El JSON usa un nombre más descriptivo.
                "UseVoiceActivity" => nameof(GuildPermission.UseVAD),
                _ => configuredName
            };

            if (!Enum.TryParse<GuildPermission>(
                    enumName,
                    ignoreCase: true,
                    out var permission))
            {
                throw new InvalidOperationException(
                    $"Unknown Discord permission: {configuredName}.");
            }

            if (permission == GuildPermission.Administrator &&
                !configuredRole.AssignToGuildOwner)
            {
                throw new InvalidOperationException(
                    "Administrator can only be granted to a role assigned to the guild owner.");
            }

            rawValue |= (ulong)permission;
        }

        return new GuildPermissions(rawValue);
    }

    private static Color ParseColor(string value)
    {
        if (value.Equals("DEFAULT", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("NONE", StringComparison.OrdinalIgnoreCase))
        {
            return Color.Default;
        }

        if (value.Length != 7 || value[0] != '#')
        {
            throw new InvalidOperationException(
                $"Invalid Discord role color: {value}.");
        }

        if (!uint.TryParse(
                value[1..],
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var rawColor))
        {
            throw new InvalidOperationException(
                $"Invalid Discord role color: {value}.");
        }

        return new Color(rawColor);
    }
}
