namespace SNEStorage.Components;

internal static class PublicFileUrl
{
    public static string Resolve(string? storedPath, string fallback)
    {
        if (string.IsNullOrWhiteSpace(storedPath)) return fallback;

        var path = storedPath.Trim().Replace('\\', '/');
        if (Uri.TryCreate(path, UriKind.Absolute, out var absoluteUri) &&
            (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
        {
            return path;
        }

        path = path.TrimStart('/');
        const string webRootPrefix = "wwwroot/";
        if (path.StartsWith(webRootPrefix, StringComparison.OrdinalIgnoreCase))
        {
            path = path[webRootPrefix.Length..];
        }

        return path.Length == 0 ? fallback : $"/{path}";
    }
}
