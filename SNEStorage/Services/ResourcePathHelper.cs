using SNEStorage.Models;
using System.Text.RegularExpressions;

namespace SNEStorage.Services;

internal static partial class ResourcePathHelper
{
    public static string GetMainResourceFolder(Resource resource)
    {
        return string.Join('/', resource.ResourceVersions.First().File!.URL.Split('/')[..3]);
    }

    public static string SanitizeFolderName(string? name)
    {
        string sanitized = InvalidCharacterRegex().Replace(name ?? "", "_");
        sanitized = sanitized.Trim().TrimEnd(' ', '.');

        if (sanitized.Length == 0)
            return "folder";

        if (ReservedNameRegex().IsMatch(sanitized))
            sanitized = "_" + sanitized;

        return sanitized;
    }

    [GeneratedRegex(@"^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\.|$)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ReservedNameRegex();

    [GeneratedRegex(@"[<>:""/\\|?*\x00-\x1F]")]
    private static partial Regex InvalidCharacterRegex();
}
