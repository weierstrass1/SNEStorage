using System.Text.RegularExpressions;

namespace SNEStorage.Components.Pages;

internal static class SocialProfileLinkRules
{
    private static readonly Regex GenericHandle = new("\\A[A-Za-z0-9][A-Za-z0-9._-]{0,63}\\z", RegexOptions.Compiled);
    private static readonly Regex PatreonHandle = new("\\A[A-Za-z0-9][A-Za-z0-9_-]{0,63}\\z", RegexOptions.Compiled);
    private static readonly Regex PaypalHandle = new("\\A[A-Za-z0-9][A-Za-z0-9_-]{0,19}\\z", RegexOptions.Compiled);
    private static readonly Regex DiscordUsername = new("\\A(?!.*\\.\\.)[a-z0-9._]{2,32}\\z", RegexOptions.Compiled);
    private static readonly Regex NumericId = new("\\A[0-9]{17,20}\\z", RegexOptions.Compiled);
    private static readonly Regex TwitterHandle = new("\\A[A-Za-z0-9_]{1,15}\\z", RegexOptions.Compiled);
    private static readonly Regex FacebookHandle = new("\\A[A-Za-z0-9.]{5,50}\\z", RegexOptions.Compiled);
    private static readonly Regex YoutubeHandle = new("\\A[A-Za-z0-9][A-Za-z0-9._-]{1,28}[A-Za-z0-9]\\z", RegexOptions.Compiled);
    private static readonly Regex InstagramHandle = new("\\A(?!.*\\.\\.)[A-Za-z0-9._]{1,30}\\z", RegexOptions.Compiled);
    private static readonly Regex SteamId = new("\\A[0-9]{17}\\z", RegexOptions.Compiled);
    private static readonly Regex SteamCustomId = new("\\A[A-Za-z0-9][A-Za-z0-9_-]{1,31}\\z", RegexOptions.Compiled);
    // Profile links can belong to legacy accounts with shorter names.
    private static readonly Regex TwitchHandle = new("\\A[A-Za-z0-9_]{1,25}\\z", RegexOptions.Compiled);
    private static readonly Regex GithubHandle = new("\\A(?=.{1,39}\\z)[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*\\z", RegexOptions.Compiled);
    private static readonly Regex NintendoNetworkId = new("\\A[A-Za-z0-9_-]{6,16}\\z", RegexOptions.Compiled);
    private static readonly Regex FacebookNumericId = new("\\A[0-9]+\\z", RegexOptions.Compiled);

    public static bool TryNormalize(string platform, string? value, out string normalized, out string error)
    {
        normalized = string.Empty;
        error = string.Empty;
        var input = value?.Trim();
        if (string.IsNullOrEmpty(input))
        {
            error = $"Enter a {platform} profile link.";
            return false;
        }

        if (platform == "Nintendo Network")
        {
            if (!NintendoNetworkId.IsMatch(input))
            {
                error = "Nintendo Network IDs must be 6–16 letters, numbers, underscores, or hyphens.";
                return false;
            }

            normalized = input;
            return true;
        }

        if (platform == "Discord" && !input.Contains('/') && !input.Contains(':'))
        {
            var username = input.TrimStart('@').ToLowerInvariant();
            if (!DiscordUsername.IsMatch(username))
            {
                error = "Enter your Discord username: 2–32 letters, numbers, underscores, or periods, without consecutive periods.";
                return false;
            }
            normalized = username;
            return true;
        }

        var candidate = BuildCandidate(platform, input);
        if (candidate is null || !Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !uri.IsDefaultPort ||
            !HasValidProfilePath(platform, uri))
        {
            error = GetInvalidLinkMessage(platform);
            return false;
        }

        var secureUri = new UriBuilder(uri)
        {
            Scheme = Uri.UriSchemeHttps,
            Port = -1,
            Query = GetProfileQuery(platform, uri),
            Fragment = string.Empty,
            Path = GetProfilePath(platform, uri)
        }.Uri;
        normalized = secureUri.AbsoluteUri.TrimEnd('/');
        return true;
    }

    public static string GetIconClass(string platform) => platform switch
    {
        "Patreon" => "patreon",
        "PayPal" => "paypal",
        "Buy Me a Coffee" => "coffee",
        "Discord" => "discord",
        "Twitter / X" => "x",
        "Facebook" => "facebook",
        "YouTube" => "youtube",
        "Instagram" => "instagram",
        "Steam" => "steam",
        "Twitch" => "twitch",
        "GitHub" => "github",
        "Nintendo Network" => "nintendo",
        _ => "generic"
    };

    public static string GetDisplayText(string platform, string? value)
    {
        var input = value?.Trim();
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        if (platform == "Nintendo Network")
        {
            return input;
        }

        if (!TryNormalize(platform, input, out var normalized, out _))
        {
            return platform == "Discord" ? "Discord username needs updating" : "Link format needs updating";
        }
        if (platform == "Discord" && !normalized.StartsWith("https://", StringComparison.Ordinal))
            return $"@{normalized}";
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri)) return "Link format needs updating";

        var segments = GetPathSegments(uri);
        return platform switch
        {
            "Patreon" when segments.Length == 1 && EqualsIgnoreCase(segments[0], "user") => "Patreon profile",
            "Patreon" => segments[^1],
            "PayPal" => $"paypal.me/{segments[^1]}",
            "Discord" => "Discord user profile",
            "YouTube" when segments.Length > 0 && segments[0].StartsWith('@') => segments[0],
            "YouTube" => "YouTube channel",
            "Facebook" when segments.Length > 0 && (EqualsIgnoreCase(segments[0], "profile.php") || EqualsIgnoreCase(segments[0], "people")) => "Facebook profile",
            "Facebook" => $"@{segments[0]}",
            "Twitter / X" or "Instagram" or "Twitch" => $"@{segments[0].TrimStart('@')}",
            "Buy Me a Coffee" or "GitHub" => segments[^1],
            "Steam" => "Steam profile",
            _ => "Open profile"
        };
    }

    private static string? BuildCandidate(string platform, string input)
    {
        if (input.Contains("://", StringComparison.Ordinal))
        {
            return input;
        }

        if (input.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ||
            input.Contains('/') || IsKnownHost(platform, input))
        {
            return $"https://{input}";
        }

        var handle = input.StartsWith('@') ? input[1..] : input;
        if (string.IsNullOrWhiteSpace(handle) || !IsValidHandle(platform, handle))
        {
            return null;
        }

        return platform switch
        {
            "Patreon" => $"https://www.patreon.com/{handle}",
            "PayPal" => $"https://www.paypal.me/{handle}",
            "Buy Me a Coffee" => $"https://www.buymeacoffee.com/{handle}",
            "Twitter / X" => $"https://x.com/{handle}",
            "Facebook" => $"https://www.facebook.com/{handle}",
            "YouTube" => $"https://www.youtube.com/@{handle}",
            "Instagram" => $"https://www.instagram.com/{handle}",
            "Steam" => $"https://steamcommunity.com/id/{handle}",
            "Twitch" => $"https://www.twitch.tv/{handle}",
            "GitHub" => $"https://github.com/{handle}",
            _ => null
        };
    }

    private static bool IsKnownHost(string platform, string input)
    {
        var host = input.TrimEnd('/').ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        return platform switch
        {
            "Patreon" => host == "patreon.com",
            "PayPal" => host is "paypal.me" or "paypal.com",
            "Buy Me a Coffee" => host == "buymeacoffee.com",
            "Discord" => host is "discord.gg" or "discord.com" or "discordapp.com",
            "Twitter / X" => host is "x.com" or "twitter.com",
            "Facebook" => host is "facebook.com" or "fb.com" or "m.facebook.com" or "web.facebook.com",
            "YouTube" => host is "youtube.com" or "m.youtube.com",
            "Instagram" => host == "instagram.com",
            "Steam" => host == "steamcommunity.com",
            "Twitch" => host == "twitch.tv",
            "GitHub" => host == "github.com",
            _ => false
        };
    }

    private static bool HasValidProfilePath(string platform, Uri uri)
    {
        var host = uri.IdnHost.ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host[4..];
        }

        var segments = GetPathSegments(uri);
        return platform switch
        {
            "Patreon" => host == "patreon.com" && HasValidPatreonPath(uri, segments),
            "PayPal" => host == "paypal.me" && HasOneHandle(segments, PaypalHandle) ||
                        host == "paypal.com" && segments.Length == 2 && EqualsIgnoreCase(segments[0], "paypalme") && Matches(PaypalHandle, segments[1]),
            "Buy Me a Coffee" => host == "buymeacoffee.com" && HasOneHandle(segments, GenericHandle),
            "Discord" => (host is "discord.com" or "discordapp.com") && segments.Length == 2 &&
                         EqualsIgnoreCase(segments[0], "users") && NumericId.IsMatch(segments[1]),
            "Twitter / X" => (host is "x.com" or "twitter.com") && HasOneHandle(segments, TwitterHandle),
            "Facebook" => (host is "facebook.com" or "fb.com" or "m.facebook.com" or "web.facebook.com") &&
                           (segments.Length == 1 && (EqualsIgnoreCase(segments[0], "profile.php")
                               ? TryGetNumericQueryValue(uri, "id", out _)
                               : Matches(FacebookHandle, segments[0])) ||
                            segments.Length == 3 && EqualsIgnoreCase(segments[0], "people") &&
                            Matches(GenericHandle, segments[1]) && FacebookNumericId.IsMatch(segments[2])),
            "YouTube" => (host is "youtube.com" or "m.youtube.com") &&
                         (segments.Length == 1 && segments[0].StartsWith('@') && Matches(YoutubeHandle, segments[0][1..]) ||
                          segments.Length == 2 && IsYoutubeChannelRoute(segments[0]) && Matches(GenericHandle, segments[1])),
            "Instagram" => host == "instagram.com" && HasOneHandle(segments, InstagramHandle) &&
                            !segments[0].StartsWith('.') && !segments[0].EndsWith('.'),
            "Steam" => host == "steamcommunity.com" && segments.Length == 2 &&
                       (EqualsIgnoreCase(segments[0], "id") && Matches(SteamCustomId, segments[1]) ||
                        EqualsIgnoreCase(segments[0], "profiles") && SteamId.IsMatch(segments[1])),
            "Twitch" => host == "twitch.tv" &&
                        (HasOneHandle(segments, TwitchHandle) || segments.Length == 2 && Matches(TwitchHandle, segments[0]) &&
                         (segments[1] is "about" or "schedule" or "videos" or "clips")),
            "GitHub" => host == "github.com" && HasOneHandle(segments, GithubHandle),
            _ => false
        };
    }

    private static bool HasValidPatreonPath(Uri uri, string[] segments)
    {
        if (segments.Length == 0)
        {
            return false;
        }

        if (EqualsIgnoreCase(segments[0], "user"))
        {
            return segments.Length == 1 && TryGetNumericQueryValue(uri, "u", out _);
        }

        var handleIndex = segments[0] is "c" or "cw" ? 1 : 0;
        return segments.Length > handleIndex && Matches(PatreonHandle, segments[handleIndex]) &&
               (segments.Length == handleIndex + 1 ||
                segments.Length == handleIndex + 2 && (segments[^1] is "about" or "posts" or "membership" or "home"));
    }

    private static string GetProfilePath(string platform, Uri uri)
    {
        var segments = GetPathSegments(uri);
        if (platform == "Patreon" && !EqualsIgnoreCase(segments[0], "user"))
        {
            var profileSegmentCount = segments[0] is "c" or "cw" ? 2 : 1;
            segments = segments.Take(profileSegmentCount).ToArray();
        }
        else if (platform == "Twitch")
        {
            segments = segments.Take(1).ToArray();
        }

        return "/" + string.Join('/', segments.Select(segment => Uri.EscapeDataString(segment).Replace("%40", "@")));
    }

    private static string GetProfileQuery(string platform, Uri uri)
    {
        var segments = GetPathSegments(uri);
        if (platform == "Patreon" && EqualsIgnoreCase(segments[0], "user") && TryGetNumericQueryValue(uri, "u", out var userId))
        {
            return $"u={userId}";
        }

        return platform == "Facebook" && EqualsIgnoreCase(segments[0], "profile.php") && TryGetNumericQueryValue(uri, "id", out var facebookId)
            ? $"id={facebookId}"
            : string.Empty;
    }

    private static bool IsValidHandle(string platform, string value) => platform switch
    {
        "Patreon" => PatreonHandle.IsMatch(value),
        "PayPal" => PaypalHandle.IsMatch(value),
        "Buy Me a Coffee" => Matches(GenericHandle, value),
        "Twitter / X" => TwitterHandle.IsMatch(value),
        "Facebook" => FacebookHandle.IsMatch(value),
        "YouTube" => Matches(YoutubeHandle, value),
        "Instagram" => InstagramHandle.IsMatch(value) && !value.StartsWith('.') && !value.EndsWith('.'),
        "Steam" => SteamCustomId.IsMatch(value),
        "Twitch" => TwitchHandle.IsMatch(value),
        "GitHub" => GithubHandle.IsMatch(value),
        _ => false
    };

    private static bool IsYoutubeChannelRoute(string route) =>
        EqualsIgnoreCase(route, "channel") || EqualsIgnoreCase(route, "c") || EqualsIgnoreCase(route, "user");

    private static bool TryGetNumericQueryValue(Uri uri, string key, out string value)
    {
        value = string.Empty;
        var matches = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries)
            .Select(pair => pair.Split('=', 2))
            .Where(pair => pair.Length == 2 && EqualsIgnoreCase(Uri.UnescapeDataString(pair[0]), key))
            .ToArray();
        if (matches.Length != 1)
        {
            return false;
        }

        value = Uri.UnescapeDataString(matches[0][1]);
        return FacebookNumericId.IsMatch(value);
    }

    private static string[] GetPathSegments(Uri uri) => uri.AbsolutePath
        .Split('/', StringSplitOptions.RemoveEmptyEntries)
        .Select(Uri.UnescapeDataString)
        .ToArray();

    private static bool HasOneHandle(string[] segments, Regex pattern) =>
        segments.Length == 1 && Matches(pattern, segments[0]);

    private static bool Matches(Regex pattern, string value) => pattern.IsMatch(value);

    private static bool EqualsIgnoreCase(string value, string expected) =>
        value.Equals(expected, StringComparison.OrdinalIgnoreCase);

    private static string GetInvalidLinkMessage(string platform) => platform switch
    {
        "Patreon" => "Enter a Patreon username, patreon.com/c/username, or patreon.com/user?u=ID profile link.",
        "PayPal" => "Enter a PayPal.Me username or a paypal.me profile link.",
        "Buy Me a Coffee" => "Enter a Buy Me a Coffee username or profile link.",
        "Discord" => "Enter your Discord username or a discord.com/users/ID profile link. Server invites are not accepted.",
        "Twitter / X" => "Enter a valid X or Twitter username (up to 15 letters, numbers, or underscores) or profile link.",
        "Facebook" => "Enter a Facebook username or a facebook.com/profile.php?id=... link.",
        "YouTube" => "Enter a valid YouTube handle or channel link.",
        "Instagram" => "Enter a valid Instagram username or profile link.",
        "Steam" => "Enter a Steam Community /id/ or /profiles/ link.",
        "Twitch" => "Enter a valid Twitch username or profile link.",
        "GitHub" => "Enter a valid GitHub username or profile link.",
        _ => "Enter a valid profile link."
    };
}
