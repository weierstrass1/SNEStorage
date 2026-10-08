namespace SNEStorage.Components.Resources;

internal static class ResourceFileLinks
{
    // HTTP contract agreed with the backend owner. Never expose a physical Storage path.
    public static string? For(long resourceId, long resourceFileId, bool download = false) =>
        resourceId > 0 && resourceFileId > 0
            ? $"/api/resource/{resourceId}/file/{resourceFileId}{(download ? "/download" : string.Empty)}"
            : null;

    public static string? External(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) &&
        !string.IsNullOrWhiteSpace(uri.Host) && string.IsNullOrEmpty(uri.UserInfo)
            ? uri.AbsoluteUri
            : null;
}
