using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

public sealed class ResourceController(
    SnestorageContext context,
    FileService files,
    ResourceAccessService access) : Controller
{
    [HttpGet("/Resource/Download")]
    public async Task<IActionResult> Download(long id, CancellationToken cancellationToken)
    {
        var resource = await context.Resources
            .Include(item => item.Visibility)
            .Include(item => item.File)
                .ThenInclude(file => file!.FileType)
            .FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

        if (resource?.File is null || string.IsNullOrWhiteSpace(resource.File.URL))
            return NotFound();
        if (!await access.CanViewAsync(resource, User, directLink: true, cancellationToken))
            return Forbid();

        string physicalPath;
        try
        {
            physicalPath = files.ResolveStoragePath(resource.File.URL);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }

        if (!System.IO.File.Exists(physicalPath))
            return NotFound();

        var baseName = string.IsNullOrWhiteSpace(resource.Name)
            ? Path.GetFileNameWithoutExtension(physicalPath)
            : resource.Name;
        var safeBaseName = string.Concat(baseName.Split(Path.GetInvalidFileNameChars()));
        var downloadName = safeBaseName + Path.GetExtension(physicalPath);
        var contentType = GetContentType(resource.File.FileType?.Value, physicalPath);

        resource.Downloads = (resource.Downloads ?? 0) + 1;
        await context.SaveChangesAsync(cancellationToken);

        return PhysicalFile(physicalPath, contentType, downloadName, enableRangeProcessing: true);
    }

    [HttpGet("/Resource/Media/{fileId:long}")]
    public async Task<IActionResult> Preview(long fileId, long resourceId, CancellationToken cancellationToken)
    {
        var resource = await context.Resources
            .Include(item => item.Visibility)
            .FirstOrDefaultAsync(item => item.Id == resourceId, cancellationToken);
        if (resource is null)
            return NotFound();
        if (!await access.CanViewAsync(resource, User, directLink: true, cancellationToken))
            return Forbid();

        var isAttached = resource.FileId == fileId || await context.ResourcesMedia
            .AnyAsync(media => media.ResourceId == resourceId &&
                (media.FileId == fileId || media.PosterFileId == fileId), cancellationToken);
        if (!isAttached)
            return NotFound();

        var file = await context.Files
            .Include(item => item.FileType)
            .FirstOrDefaultAsync(item => item.Id == fileId, cancellationToken);
        if (file is null || string.IsNullOrWhiteSpace(file.URL))
            return NotFound();

        string physicalPath;
        try
        {
            physicalPath = files.ResolveStoragePath(file.URL);
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }

        return System.IO.File.Exists(physicalPath)
            ? PhysicalFile(physicalPath, GetContentType(file.FileType?.Value, physicalPath), enableRangeProcessing: true)
            : NotFound();
    }

    private static string GetContentType(string? configuredType, string path)
    {
        if (!string.IsNullOrWhiteSpace(configuredType))
            return configuredType;

        var provider = new FileExtensionContentTypeProvider();
        return provider.TryGetContentType(path, out var contentType)
            ? contentType
            : "application/octet-stream";
    }
}
