using MediaInfo;
using MediaInfo.Analysis;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneOf;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SkiaSharp;
using SNEStorage.Models;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;

namespace SNEStorage.Services;
public enum FileContext
{
    Avatar,
    Previews,
    Resource
}
public enum FileServiceResult
{
    Success,
    InvalidFileForContext,
    FileCreationError,
    CancellationTokenError,
    ImageSurpassFramesLimit,
    InvalidImageSize,
    InvalidImageNumberOfFrames,
    InvalidImage,
    FormReadingError,
    InvalidMime,
    MimeMismatch,
    CacheError,
    BatchError,
    DBError,
    UpdateBatchError,
    InvalidVideo,
    InvalidFile,
    EmptyFile,
    MediaSizeSurpassTheLimit,
    CompressedFileSizeSurpassTheLimit,
    FileSizeSurpassTheLimit,
    AvatarSizeSurpassTheLimit
}
public class FileService(SnestorageContext context,
    CacheService cacheService,
    DBHelperService dbHelperService,
    IWebHostEnvironment environment)
{
    public const long MAX_FILE_SIZE = 250 * 1024 * 1024;
    public const long MAX_COMPRESSED_FILE_SIZE = 250 * 1024 * 1024;
    public const long MAX_MEDIA_FILE_SIZE = 5 * 1024 * 1024;
    public const long MAX_AVATAR_FILE_SIZE = 5 * 1024 * 1024;
    public const long MAX_IMAGE_PIXELS = 4194304;
    public const long MAX_IMAGE_FRAMES = 1800;
    public const int MAX_ARCHIVE_ENTRIES = 10000;

    private readonly SnestorageContext _context = context;
    private readonly CacheService _cacheService = cacheService;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly IWebHostEnvironment _environment = environment;
    private static readonly IMediaInfoAnalyzer _mediaAnalyzer = MediaInfoAnalyzer.CreateDefault();
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
    public bool FinishDeletion(IEnumerable<Models.File> files)
    {
        bool allDeleted = true;

        foreach (var file in files)
        {
            bool deleted = _dbHelperService.TryFunction(() =>
            {
                var path = Path.Combine(
                    _environment.ContentRootPath, file.URL);

                System.IO.File.Delete(path);
                return true;
            });

            allDeleted &= deleted;
        }

        return allDeleted;
    }
    public async Task<OneOf<Models.File, FileServiceResult>> PrepareFile(FileContext context, IFormFile fileRequest, string destinationPath, string batchPath, CancellationToken ct)
    {
        if(!ValidateContext(context, fileRequest))
        {
            await BatchRollback(batchPath, ct);
            return FileServiceResult.InvalidFileForContext;
        }
        var res = await ValidateFile(fileRequest, ct);
        if (res != FileServiceResult.Success)
        {
            await BatchRollback(batchPath, ct);
            return res;
        }

        string phisicalPath = Path.Combine(_environment.ContentRootPath, destinationPath);

        if (!await UpdateBatch(phisicalPath, batchPath, ct))
        {
            await BatchRollback(batchPath, ct);
            return FileServiceResult.UpdateBatchError;
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
            return FileServiceResult.FileCreationError;
        }

        var alltypes = await _cacheService.GetAllFileTypesByMime(ct);

        if (alltypes == null)
        {
            await BatchRollback(batchPath, ct);
            return FileServiceResult.CacheError;
        }

        var type = alltypes[fileRequest.ContentType.ToLowerInvariant().Trim()];

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
    public bool ValidateContext(FileContext context, IFormFile form)
    {
        string contentType = form.ContentType?.Trim().ToLowerInvariant() ?? "";
        if (string.IsNullOrWhiteSpace(contentType))
            return false;
        return context switch
        {
            FileContext.Avatar => contentType.StartsWith("image/", StringComparison.Ordinal),
            FileContext.Previews => contentType.StartsWith("image/", StringComparison.Ordinal) ||
                                contentType.StartsWith("video/", StringComparison.Ordinal),
            FileContext.Resource => contentType.StartsWith("application/", StringComparison.Ordinal),
            _ => false,
        };
    }
    public async Task<FileServiceResult> ValidateFile(IFormFile form, CancellationToken ct = default)
    {
        if (form.Length <= 0)
            return FileServiceResult.EmptyFile;
        if (form.Length > MAX_FILE_SIZE)
            return FileServiceResult.FileSizeSurpassTheLimit;

        string? expectedMime = await GetExpectedMime(form, ct);
        if (expectedMime is null)
            return FileServiceResult.CacheError;
        if (string.IsNullOrWhiteSpace(expectedMime))
            return FileServiceResult.InvalidMime;

        bool isImage = expectedMime.StartsWith("image/", StringComparison.Ordinal);
        bool isVideo = expectedMime.StartsWith("video/", StringComparison.Ordinal);

        if ((isImage || isVideo) && form.Length > MAX_MEDIA_FILE_SIZE)
            return FileServiceResult.MediaSizeSurpassTheLimit;

        using var stream = form.OpenReadStream();
        if (!stream.CanRead || !stream.CanSeek || stream.Length != form.Length)
            return FileServiceResult.FormReadingError;


        Func<Stream, Task<OneOf<string, FileServiceResult>>> validateFunc = isImage ?
            async s => InspectImage(s, ct) :
            isVideo ?
                async s => await InspectVideo(s, ct) :
                async s => InspectArchive(s, ct);

        var res = await validateFunc(stream);
        if (res.TryPickT1(out var error, out string? detectedMime))
            return error;

        if (detectedMime is null || !MimeEquals(detectedMime, expectedMime))
            return FileServiceResult.MimeMismatch;

        return FileServiceResult.Success;
    }
    public async Task<OneOf<T1, T2>> TrySyncContextWithFiles<T1, T2>(string batchPath, T1 res, T2 error, CancellationToken ct = default)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
            BatchComplete(batchPath);
            return res;
        }
        catch
        {
            await BatchRollback(batchPath, ct);
            return error;
        }
    }
    public async Task<bool> TrySyncContextWithFiles(string batchPath, CancellationToken ct = default)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
            BatchComplete(batchPath);
            return true;
        }
        catch
        {
            await BatchRollback(batchPath, ct);
            return false;
        }
    }
    private async Task<string?> GetExpectedMime(IFormFile form, CancellationToken ct = default)
    {
        var filetypes = await _cacheService.GetAllFileTypesByMime(ct);
        if (filetypes == null)
            return null;

        string mime = form.ContentType.ToLowerInvariant().Trim();

        return filetypes.ContainsKey(mime) ?
            mime : "";
    }
    private static OneOf<string, FileServiceResult> InspectImage(Stream stream, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return FileServiceResult.CancellationTokenError;

        using var codec = SKCodec.Create(stream);
        if (codec is null)
            return FileServiceResult.InvalidImage;

        string? mime = codec.EncodedFormat switch
        {
            SKEncodedImageFormat.Png => "image/png",
            SKEncodedImageFormat.Jpeg => "image/jpeg",
            SKEncodedImageFormat.Gif => "image/gif",
            SKEncodedImageFormat.Webp => "image/webp",
            SKEncodedImageFormat.Avif => "image/avif",
            _ => null
        };

        if (mime is null)
            return FileServiceResult.InvalidMime;

        if (codec.FrameCount < 0)
            return FileServiceResult.InvalidImageNumberOfFrames;

        var source = codec.Info;
        if (source.Width <= 0 || source.Height <= 0 || (long)source.Width * source.Height > MAX_IMAGE_PIXELS)
            return FileServiceResult.InvalidImageSize;

        if (codec.FrameCount > MAX_IMAGE_FRAMES)
            return FileServiceResult.ImageSurpassFramesLimit;

        var info = new SKImageInfo(
            source.Width, source.Height,
            SKColorType.Rgba8888, SKAlphaType.Premul);

        if (ct.IsCancellationRequested)
            return FileServiceResult.CancellationTokenError;

        using var bitmap = new SKBitmap(info);
        if (bitmap.GetPixels() == IntPtr.Zero)
            return FileServiceResult.InvalidImageSize;

        int frameCount = Math.Max(1, codec.FrameCount);

        for (int frame = 0; frame < frameCount; frame++)
        {
            if (ct.IsCancellationRequested)
                return FileServiceResult.CancellationTokenError;

            bitmap.Erase(SKColors.Transparent);

            var options = new SKCodecOptions(frame) { PriorFrame = -1 };
            var result = codec.GetPixels(
                info, bitmap.GetPixels(), bitmap.RowBytes, options);

            if (result != SKCodecResult.Success)
                return FileServiceResult.InvalidImageNumberOfFrames;
        }

        return mime;
    }

    private async Task<OneOf<string, FileServiceResult>> InspectVideo(
        Stream stream, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return FileServiceResult.CancellationTokenError;

        var media = await _dbHelperService.TryFunction(
            async cancel => await _mediaAnalyzer.AnalyzeAsync(stream, true, cancel),
            ct: ct);
        if (media == null)
            return ct.IsCancellationRequested ?
                FileServiceResult.CancellationTokenError :
                FileServiceResult.InvalidVideo;

        if (!media.Success || !media.HasVideo || !media.VideoStreams.Any())
            return FileServiceResult.InvalidVideo;

        if (media.VideoStreams.Any(video =>
            video.Width <= 0 || video.Height <= 0))
            return FileServiceResult.InvalidVideo;

        string? mime = media.General.Format switch
        {
            "MPEG-4" => "video/mp4",
            "WebM" => "video/webm",
            "Ogg" => "video/ogg",
            _ => null
        };

        if (mime is null)
            return FileServiceResult.InvalidMime;

        return mime;
    }

    private OneOf<string, FileServiceResult> InspectArchive(Stream stream, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return FileServiceResult.CancellationTokenError;

        if (!stream.CanRead || !stream.CanSeek)
            return FileServiceResult.FormReadingError;

        if (stream.Length == 0)
            return FileServiceResult.EmptyFile;

        if (!ArchiveFactory.IsArchive(stream, out var archiveType))
            return FileServiceResult.InvalidFile;

        string? mime = archiveType switch
        {
            ArchiveType.Zip => "application/zip",
            ArchiveType.Rar => "application/vnd.rar",
            ArchiveType.SevenZip => "application/x-7z-compressed",
            ArchiveType.Tar => "application/x-tar",
            ArchiveType.GZip => "application/gzip",
            _ => null
        };

        if (mime is null)
            return FileServiceResult.InvalidMime;

        if (ct.IsCancellationRequested)
            return FileServiceResult.CancellationTokenError;

        stream.Position = 0;
        using var archive = _dbHelperService.TryFunction(
            () => ArchiveFactory.OpenArchive(stream, new ReaderOptions
            {
                LeaveStreamOpen = true,
                LookForHeader = false
            }));

        if (archive == null)
            return FileServiceResult.FormReadingError;

        if (archive.Type != archiveType)
            return FileServiceResult.InvalidFile;

        var entries = _dbHelperService.TryFunction(() => archive.Entries
             .Take(MAX_ARCHIVE_ENTRIES + 1)
             .TakeWhile(_ => !ct.IsCancellationRequested)
             .ToArray());

        if (ct.IsCancellationRequested)
            return FileServiceResult.CancellationTokenError;

        if (entries is null)
            return FileServiceResult.InvalidFile;

        if (entries.Length > MAX_ARCHIVE_ENTRIES ||
            entries.Any(entry => entry.IsEncrypted || !entry.IsComplete ||
                entry.IsSplitAfter))
            return FileServiceResult.InvalidFile;

        return entries.Any(entry => !entry.IsDirectory) ?
            mime : FileServiceResult.InvalidFile;
    }
    private static bool MimeEquals(string first, string second)
    {
        static string Canonicalize(string mime)
        {
            return mime.Trim().ToLowerInvariant() switch
            {
                "application/x-zip-compressed" => "application/zip",
                "application/x-gzip" => "application/gzip",
                var value => value
            };
        }

        return Canonicalize(first) == Canonicalize(second);
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
