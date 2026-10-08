using Microsoft.EntityFrameworkCore;
using SNEStorage.Models;
using System.Text.Json;

namespace SNEStorage.Services;

public class FileService(SnestorageContext context,
    IWebHostEnvironment environment)
{
    private readonly SnestorageContext _context = context;
    private readonly IWebHostEnvironment _environment = environment;
    public async Task<string?> BeginBatch(CancellationToken ct)
    {
        try
        {
            string phisicalPath = GenerateBatchPath();
            Directory.CreateDirectory(Path.GetDirectoryName(phisicalPath)!);
            await System.IO.File.WriteAllTextAsync(phisicalPath, "[]", ct);
            return phisicalPath;
        }
        catch
        {
            return null;
        }
    }
    public async Task<List<Models.File>> PrepareDeletion(IEnumerable<long> fileIds, CancellationToken ct = default)
    {
        var files = await _context
            .Files
            .AsNoTracking()
            .Where(f => fileIds.Contains(f.Id))
            .ToListAsync(ct);
        _context.Files.RemoveRange(files);
        return files;
    }
    public async void FinishDeletion(IEnumerable<Models.File> files)
    {
        foreach (var file in files.Select(f => Path.Combine(_environment.ContentRootPath, f.URL)))
        {
            if (System.IO.File.Exists(file))
                System.IO.File.Delete(file);
        }
    }
    public async Task<Models.File?> PrepareFile(IFormFile fileRequest, string destinationPath, string batchPath, CancellationToken ct)
    {
        var filetype = fileRequest.ContentType;

        FileType? type = null;

        try
        {
            type = await _context.FileTypes
                .FirstOrDefaultAsync(f => string.Compare(f.Value.ToLower(), filetype.ToLower().Trim()) == 0, ct);
        }
        catch
        {
        }

        if (type is null)
        {
            await BatchRollback(batchPath, ct);
            return null;
        }

        string phisicalPath = Path.Combine(_environment.ContentRootPath, destinationPath);

        if (!await UpdateBatch(phisicalPath, batchPath, ct))
        {
            await BatchRollback(batchPath, ct);
            return null;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(phisicalPath)!);
            await using var stream = System.IO.File.Create(phisicalPath);
            await fileRequest.CopyToAsync(stream, ct);
        }
        catch
        {
            await BatchRollback(batchPath, ct);
            return null;
        }

        var file = new Models.File
        {
            URL = destinationPath.Replace('\\', '/'),
            FileTypeId = type.Id,
        };

        _context.Files.Add(file);

        return file;
    }
    public void BatchComplete(string batchPath)
    {
        try
        {
            if (System.IO.File.Exists(batchPath))
                System.IO.File.Delete(batchPath);
        }
        catch
        {

        }
    }
    public async Task BatchRollback(string batchPath, CancellationToken ct)
    {
        try
        {
            _context.ChangeTracker.Clear();
            string batchContent = await System.IO.File.ReadAllTextAsync(batchPath, ct);
            var batchList = JsonSerializer.Deserialize<List<string>>(batchContent);
            List<string> files = [.. batchList!
            .Select(f => f.Trim())
            .Where(f => System.IO.File.Exists(f))];
            foreach (var file in files)
            {
                System.IO.File.Delete(file);
            }
            System.IO.File.Delete(batchPath);
        }
        catch
        {

        }
    }
    private static async Task<bool> UpdateBatch(string phisicalPath, string batchPath, CancellationToken ct)
    {
        try
        {
            string batchContent = await System.IO.File.ReadAllTextAsync(batchPath, ct);
            var batchList = JsonSerializer.Deserialize<List<string>>(batchContent);
            batchList!.Add(phisicalPath);
            batchContent = JsonSerializer.Serialize(batchList);
            await System.IO.File.WriteAllTextAsync(batchPath, batchContent, ct);
            return true;
        }
        catch
        {
            return false;
        }
    }
    private string GenerateBatchPath()
    {
        string path;
        do
        {
            path = Path.Combine(_environment.ContentRootPath, "Storage", "Batches",
                $"{Guid.NewGuid():N}.json");
        }
        while (System.IO.File.Exists(path));
        return path;
    }
}
