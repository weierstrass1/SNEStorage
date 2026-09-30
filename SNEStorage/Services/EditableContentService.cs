using System.Text;

namespace SNEStorage.Services;

public sealed class EditableContentService(IWebHostEnvironment environment)
{
    private static readonly IReadOnlyDictionary<string, string> Fallbacks = new Dictionary<string, string>
    {
        ["landing"] = "The community vault for SNES hacks, homebrew, and creative projects.",
        ["rules"] = "Only share resources you have the right to distribute.\n\nDescribe the content clearly and label any sensitive material.",
        ["disclaimer"] = "SNEStorage is a community archive. Each resource belongs to its authors; publication does not imply affiliation with or endorsement by Nintendo."
    };

    public async Task<string> ReadAsync(string section, CancellationToken cancellationToken = default)
    {
        if (!Fallbacks.TryGetValue(section, out var fallback))
            throw new ArgumentOutOfRangeException(nameof(section));

        var path = Path.Combine(environment.ContentRootPath, "Content", $"{section}.txt");
        if (!System.IO.File.Exists(path))
            return fallback;

        var content = await System.IO.File.ReadAllTextAsync(path, Encoding.UTF8, cancellationToken);
        return string.IsNullOrWhiteSpace(content) ? fallback : content.Trim();
    }
}
