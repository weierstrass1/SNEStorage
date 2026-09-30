using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Services;

public class FileService
{
    public SnestorageContext Context { get; }
    public IWebHostEnvironment Environment { get; }

    public FileService(SnestorageContext context,
        IWebHostEnvironment environment)
    {
        Context = context;
        Environment = environment;
    }
    public async Task<ActionResult<FileResponse>?> Create(IFormFile fileRequest, string destinationPath)
    {
        string filetype = fileRequest.ContentType;

        FileType? type = await Context.FileTypes
            .FirstOrDefaultAsync(f => f.Value.Contains(filetype));
        if (type == null)
            return null;
        string path = Path.Combine(Environment.WebRootPath, destinationPath);

        using (FileStream stream = System.IO.File.Create(path))
        {
            await fileRequest.CopyToAsync(stream);
        }

        Models.File file = new()
        {
            URL = destinationPath,
            FileTypeId = type.Id
        };

        Context.Add(file);
        Context.SaveChanges();

        FileResponse result = new()
        {
            Id = file.Id,
            URL = file.URL,
            FileTypeId = file.FileTypeId
        };
        return result;
    }
    public async Task<FileResponse?> CreateAsync(IFormFile fileRequest, string relativeDestinationPath, CancellationToken ct)
    {
        // Resolver tipo por ContentType
        var filetype = fileRequest.ContentType;
        var type = await Context.FileTypes
            .FirstOrDefaultAsync(f => EF.Functions.Like(f.Value, $"%{filetype}%"), ct);
        if (type is null) return null;

        // Asegurar carpeta y guardar físico en wwwroot
        var absolutePath = Path.Combine(Environment.WebRootPath, relativeDestinationPath);
        Directory.CreateDirectory(Path.GetDirectoryName(absolutePath)!);
        await using (var stream = System.IO.File.Create(absolutePath))
            await fileRequest.CopyToAsync(stream, ct);

        // Insert en [file]
        var file = new Models.File
        {
            URL = relativeDestinationPath.Replace('\\', '/'),
            FileTypeId = type.Id,
            // Si tu tabla [file] tiene estas columnas, mapéalas:
            // Name = Path.GetFileName(fileRequest.FileName),
            // Size = fileRequest.Length,
            // CreatedAt = DateTime.UtcNow
        };

        Context.Files.Add(file);
        await Context.SaveChangesAsync(ct);

        return new FileResponse
        {
            Id = file.Id,
            URL = file.URL,
            FileTypeId = file.FileTypeId
        };
    }
}
