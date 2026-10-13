namespace SNEStorage.DiscordBot.Services;

internal static class DiscordNotificationFormatting
{
    public static string SingleLine(string? value, int maxLength, string fallback)
    {
        var normalized = string.Join(
            " ",
            (value ?? string.Empty)
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

        return Truncate(
            string.IsNullOrWhiteSpace(normalized) ? fallback : normalized,
            maxLength);
    }

    public static string Body(string? value, int maxLength, string fallback)
    {
        var normalized = NormalizeBody(value);
        return Truncate(
            string.IsNullOrWhiteSpace(normalized) ? fallback : normalized,
            maxLength);
    }

    public static IReadOnlyList<string> SplitBody(
        string? value,
        int maxChunkLength,
        string fallback)
    {
        var remaining = NormalizeBody(value);
        if (string.IsNullOrWhiteSpace(remaining))
            return [fallback];

        List<string> chunks = [];

        while (remaining.Length > maxChunkLength)
        {
            var splitAt = FindSplitPosition(remaining, maxChunkLength);
            var chunk = remaining[..splitAt].TrimEnd();

            if (chunk.Length > 0)
                chunks.Add(chunk);

            remaining = remaining[splitAt..].TrimStart();
        }

        if (remaining.Length > 0)
            chunks.Add(remaining);

        return chunks.Count == 0 ? [fallback] : chunks;
    }

    public static string JoinValues(
        IEnumerable<string?> values,
        int maxLength,
        string fallback)
    {
        var joined = string.Join(
            " · ",
            values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase));

        return Truncate(
            string.IsNullOrWhiteSpace(joined) ? fallback : joined,
            maxLength);
    }

    public static DateTimeOffset Timestamp(DateTime value, DateTimeOffset fallback)
    {
        if (value == default)
            return fallback;

        return value.Kind switch
        {
            DateTimeKind.Utc => new DateTimeOffset(value),
            DateTimeKind.Local => new DateTimeOffset(value.ToUniversalTime()),
            _ => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc))
        };
    }

    private static string NormalizeBody(string? value) =>
        (value ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

    private static string Truncate(string value, int maxLength)
    {
        if (value.Length <= maxLength)
            return value;

        var contentLength = Math.Max(1, maxLength - 1);
        if (contentLength < value.Length &&
            contentLength > 0 &&
            char.IsHighSurrogate(value[contentLength - 1]))
        {
            contentLength--;
        }

        return value[..contentLength].TrimEnd() + "…";
    }

    private static int FindSplitPosition(string value, int maxChunkLength)
    {
        var splitAt = value.LastIndexOf("\n\n", maxChunkLength, StringComparison.Ordinal);
        if (splitAt < maxChunkLength / 2)
            splitAt = value.LastIndexOf('\n', maxChunkLength);
        if (splitAt < maxChunkLength / 2)
            splitAt = value.LastIndexOf(' ', maxChunkLength);
        if (splitAt <= 0)
            splitAt = maxChunkLength;

        if (splitAt < value.Length &&
            splitAt > 0 &&
            char.IsLowSurrogate(value[splitAt]))
        {
            splitAt--;
        }

        return splitAt;
    }
}
