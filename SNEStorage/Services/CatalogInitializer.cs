using Microsoft.EntityFrameworkCore;
using SNEStorage.Models;

namespace SNEStorage.Services;

public static class CatalogInitializer
{
    public static async Task SeedInMemoryAsync(SnestorageContext context, CancellationToken cancellationToken = default)
    {
        if (!await context.FileTypes.AnyAsync(cancellationToken))
        {
            var contentTypes = new[]
            {
                "application/octet-stream", "application/zip", "application/x-zip-compressed",
                "application/x-7z-compressed", "application/x-rar-compressed", "application/pdf",
                "image/png", "image/jpeg", "image/gif", "image/webp", "video/mp4", "video/webm", "video/ogg"
            };
            context.FileTypes.AddRange(contentTypes.Select(value => new FileType { Name = value, Value = value }));
        }

        if (!await context.Visibilities.AnyAsync(item => item.ResourceOnly, cancellationToken))
        {
            context.Visibilities.AddRange(
                new Visibility { Name = "Public", ResourceOnly = true },
                new Visibility { Name = "Private", ResourceOnly = true },
                new Visibility { Name = "Unlisted", ResourceOnly = true });
        }

        if (!await context.Visibilities.AnyAsync(item => !item.ResourceOnly, cancellationToken))
        {
            context.Visibilities.AddRange(
                new Visibility { Name = "Public", ResourceOnly = false },
                new Visibility { Name = "Private", ResourceOnly = false });
        }

        if (!await context.TimeZones.AnyAsync(cancellationToken))
            context.TimeZones.Add(new Models.TimeZone { Name = "UTC", Value = 0 });

        if (!await context.Videogames.AnyAsync(cancellationToken))
            context.Videogames.AddRange(
                new Videogame { Name = "Any game" },
                new Videogame { Name = "Homebrew" });

        if (!await context.ResourceTypes.AnyAsync(cancellationToken))
            context.ResourceTypes.Add(new ResourceType { Name = "Other" });

        await context.SaveChangesAsync(cancellationToken);
    }
}
