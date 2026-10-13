using System.Globalization;
using Discord;
using Discord.WebSocket;
using SNEStorage.DiscordBot.DTOs;

namespace SNEStorage.DiscordBot.Services;

public sealed record DiscordMemberSyncResult(IReadOnlyList<string> Changes);

public sealed class DiscordMemberSynchronizer(
    DiscordServerConfigLoader configLoader,
    ILogger<DiscordMemberSynchronizer> logger)
{
    private const string ColorRolePrefix = "sne-color-";
    private static readonly RequestOptions SyncRequest = new()
    {
        AuditLogReason = "SNEStorage account verification"
    };

    public async Task<DiscordMemberSyncResult> ApplyAsync(
        SocketGuildUser user,
        SNEStorageMemberStateDTO member,
        CancellationToken cancellationToken = default)
    {
        var configuration = await configLoader.LoadAsync(cancellationToken);
        var changes = new List<string>();

        var memberRole = ResolveRole(user.Guild, configuration, "member");
        var moderatorRole = ResolveRole(user.Guild, configuration, "moderator");
        var supporterRole = ResolveRole(user.Guild, configuration, "supporter");

        await SetRoleAsync(user, memberRole, true, changes);
        await SetRoleAsync(user, moderatorRole, member.IsModerator, changes);
        await SetRoleAsync(user, supporterRole, member.IsSupporter, changes);
        await SetColorRoleAsync(user, member.Color, changes);
        await SetNicknameAsync(user, member.Nickname, changes);

        logger.LogInformation(
            "Synchronized Discord member {DiscordUserId} with SNEStorage user {SNEStorageUserId}. " +
            "Changes: {Changes}.",
            user.Id,
            member.SNEStorageUserId,
            changes.Count == 0 ? "none" : string.Join(", ", changes));

        return new(changes);
    }

    private static IRole ResolveRole(
        SocketGuild guild,
        DiscordServerSetupConfigDTO configuration,
        string key)
    {
        var configured = configuration.Roles.SingleOrDefault(role =>
            string.Equals(role.Key, key, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Discord role key '{key}' is missing from server-config.json.");

        return guild.Roles.SingleOrDefault(role =>
            string.Equals(role.Name, configured.Name, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException(
                $"Discord role '{configured.Name}' does not exist. Run setup first.");
    }

    private static async Task SetRoleAsync(
        SocketGuildUser user,
        IRole role,
        bool shouldHave,
        ICollection<string> changes)
    {
        var hasRole = user.Roles.Any(existing => existing.Id == role.Id);

        if (shouldHave && !hasRole)
        {
            EnsureManageable(user.Guild, role);
            await user.AddRoleAsync(role, SyncRequest);
            changes.Add($"added {role.Name}");
        }
        else if (!shouldHave && hasRole)
        {
            EnsureManageable(user.Guild, role);
            await user.RemoveRoleAsync(role, SyncRequest);
            changes.Add($"removed {role.Name}");
        }
    }

    private static async Task SetNicknameAsync(
        SocketGuildUser user,
        string? requestedNickname,
        ICollection<string> changes)
    {
        if (string.IsNullOrWhiteSpace(requestedNickname))
            return;

        // Discord does not allow bots to change the server owner's nickname.
        if (user.Id == user.Guild.OwnerId)
            return;

        var nickname = requestedNickname.Trim();
        if (nickname.Length > 32)
            nickname = nickname[..32];

        if (string.Equals(user.Nickname, nickname, StringComparison.Ordinal))
            return;

        await user.ModifyAsync(
            properties => properties.Nickname = nickname,
            SyncRequest);

        changes.Add("updated nickname");
    }

    private static async Task SetColorRoleAsync(
        SocketGuildUser user,
        string? colorValue,
        ICollection<string> changes)
    {
        var roleName = $"{ColorRolePrefix}{user.Id}";
        IRole? role = user.Guild.Roles.SingleOrDefault(existing =>
            string.Equals(existing.Name, roleName, StringComparison.Ordinal));

        if (!TryParseColor(colorValue, out var color))
        {
            if (role is not null)
            {
                EnsureManageable(user.Guild, role);
                await role.DeleteAsync(SyncRequest);
                changes.Add("removed profile color");
            }

            return;
        }

        if (role is null)
        {
            role = await user.Guild.CreateRoleAsync(
                roleName,
                GuildPermissions.None,
                RoleColors.Solid(color),
                false,
                false,
                SyncRequest);

            changes.Add($"created profile color {colorValue}");
        }
        else if (role.Colors.PrimaryColor.RawValue != color.RawValue)
        {
            EnsureManageable(user.Guild, role);
            await role.ModifyAsync(
                properties => properties.Colors = RoleColors.Solid(color),
                SyncRequest);

            changes.Add($"updated profile color {colorValue}");
        }

        if (!user.Roles.Any(existing => existing.Id == role.Id))
        {
            EnsureManageable(user.Guild, role);
            await user.AddRoleAsync(role, SyncRequest);
            changes.Add("assigned profile color");
        }
    }

    private static bool TryParseColor(string? value, out Color color)
    {
        color = Color.Default;

        if (value is not { Length: 7 } || value[0] != '#' ||
            !uint.TryParse(
                value[1..],
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out var rawColor))
        {
            return false;
        }

        color = new Color(rawColor);
        return true;
    }

    private static void EnsureManageable(SocketGuild guild, IRole role)
    {
        if (role.IsManaged || role.Position >= guild.CurrentUser.Hierarchy)
        {
            throw new InvalidOperationException(
                $"The bot cannot manage role '{role.Name}'. " +
                "Move the SNEStorage Bot role above it.");
        }
    }
}
