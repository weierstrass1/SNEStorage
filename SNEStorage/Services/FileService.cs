using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Services;

public class FileService(SnestorageContext context, IWebHostEnvironment environment)
{
    private string StorageRoot => Path.Combine(environment.ContentRootPath, "App_Data", "files");

    public async Task<ActionResult<FileResponse>?> Create(IFormFile fileRequest, string destinationPath)
    {
        var file = await CreateAsync(fileRequest, destinationPath, CancellationToken.None);
        return file is null ? null : new ActionResult<FileResponse>(file);
    }

    public async Task<FileResponse?> CreateAsync(
        IFormFile fileRequest,
        string relativeDestinationPath,
        CancellationToken cancellationToken)
    {
        if (fileRequest.Length <= 0)
            return null;

        var contentType = string.IsNullOrWhiteSpace(fileRequest.ContentType)
            ? "application/octet-stream"
            : fileRequest.ContentType;

        var type = await context.FileTypes
            .FirstOrDefaultAsync(item => item.Value.Contains(contentType), cancellationToken);
        if (type is null)
        {
            type = new FileType { Name = contentType, Value = contentType };
            context.FileTypes.Add(type);
            await context.SaveChangesAsync(cancellationToken);
        }

        var absolutePath = ResolveStoragePath(relativeDestinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await using (var stream = System.IO.File.Create(absolutePath))
            await fileRequest.CopyToAsync(stream, cancellationToken);

        var file = new Models.File
        {
            URL = NormalizeRelativePath(relativeDestinationPath),
            FileTypeId = type.Id
        };

        context.Files.Add(file);
        await context.SaveChangesAsync(cancellationToken);

        return new FileResponse
        {
            Id = file.Id,
            URL = file.URL,
            FileTypeId = file.FileTypeId
        };
    }

    public string ResolveStoragePath(string relativePath)
    {
        var normalized = NormalizeRelativePath(relativePath);
        var root = Path.GetFullPath(StorageRoot);
        var fullPath = Path.GetFullPath(Path.Combine(root, normalized.Replace('/', Path.DirectorySeparatorChar)));
        var rootPrefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;

        if (!fullPath.StartsWith(rootPrefix, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The requested file path is outside the storage directory.");

        return fullPath;
    }

    private static string NormalizeRelativePath(string path)
    {
        var normalized = path.Replace('\\', '/').TrimStart('/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Split('/').Any(segment => segment is "" or "." or ".."))
            throw new InvalidOperationException("The file path is invalid.");

        return normalized;
    }
}
