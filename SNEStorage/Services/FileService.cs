using MediaInfo.Analysis;
using Microsoft.EntityFrameworkCore;
using OneOf;
using SharpCompress.Archives;
using SharpCompress.Common;
using SharpCompress.Readers;
using SkiaSharp;
using SNEStorage.DTOs;
using SNEStorage.Models;
using System.Text.Json;

namespace SNEStorage.Services;

public enum FileContext
{
    Avatar,
    Previews,
    Resource
}

public class FileService(SnestorageContext context, CacheService cacheService,
    DBHelperService dbHelperService, IWebHostEnvironment environment,
    ILogger<FileService> logger)
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
    private readonly ILogger<FileService> _logger = logger;
    private static readonly IMediaInfoAnalyzer _mediaAnalyzer = MediaInfoAnalyzer.CreateDefault();

    public async Task<OneOf<string, ServiceResultContext>> BeginBatch(CancellationToken ct)
    {
        string physicalPath = GenerateBatchPath();

        var directoryResult = _dbHelperService.TryFunction(
            () => Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!),
            ServiceResult.BatchCreationError);

        if (!directoryResult.TryPickT0(out _, out var directoryError))
            return directoryError;

        var writeResult = await _dbHelperService.TryFunction(
            cancel => System.IO.File.WriteAllTextAsync(physicalPath, "[]", cancel),
            ServiceResult.BatchCreationError, ct: ct);

        return writeResult.Result == ServiceResult.Success ? physicalPath : writeResult;
    }

    public async Task<OneOf<List<Models.File>, ServiceResultContext>> PrepareDeletion(
        IEnumerable<long> fileIds, CancellationToken ct = default)
    {
        var query = _context.Files.AsNoTracking()
            .Where(file => fileIds.Contains(file.Id));

        var filesResult = await _dbHelperService.TryFunction(
            cancel => query.ToListAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (!filesResult.TryPickT0(out var files, out var queryError))
            return queryError;

        _context.Files.RemoveRange(files);
        return files;
    }

    public ServiceResultContext FinishDeletion(IEnumerable<Models.File> files)
    {
        ServiceResultContext? firstError = null;

        foreach (var file in files)
        {
            string path = Path.Combine(_environment.ContentRootPath, file.URL);

            var deletionResult = _dbHelperService.TryFunction(() =>
            {
                System.IO.File.Delete(path);
                return true;
            }, ServiceResult.FileDeletionError);

            if (!deletionResult.TryPickT0(out _, out var deletionError))
                firstError ??= deletionError;
        }

        return firstError is null ? ServiceResult.Success.Return(_logger) : firstError;
    }

    public async Task<OneOf<Models.File, ServiceResultContext>> PrepareFile(
        FileContext context, IFormFile fileRequest, string destinationPath,
        string batchPath, CancellationToken ct)
    {
        if (!ValidateContext(context, fileRequest))
        {
            await BatchRollback(batchPath, ct);
            return ServiceResult.InvalidFileForContext.Return(_logger);
        }

        var validation = await ValidateFile(fileRequest, ct);

        if (validation.Result != ServiceResult.Success)
        {
            await BatchRollback(batchPath, ct);
            return validation;
        }

        string physicalPath = Path.Combine(_environment.ContentRootPath, destinationPath);
        var updateResult = await UpdateBatch(physicalPath, batchPath, ct);

        if (updateResult.Result != ServiceResult.Success)
        {
            await BatchRollback(batchPath, ct);
            return updateResult;
        }

        var directoryResult = _dbHelperService.TryFunction(
            () => Directory.CreateDirectory(Path.GetDirectoryName(physicalPath)!),
            ServiceResult.FileCreationError);

        if (!directoryResult.TryPickT0(out _, out var directoryError))
        {
            await BatchRollback(batchPath, ct);
            return directoryError;
        }

        var streamResult = _dbHelperService.TryFunction(
            () => System.IO.File.Create(physicalPath),
            ServiceResult.FileCreationError);

        if (!streamResult.TryPickT0(out var stream, out var streamError))
        {
            await BatchRollback(batchPath, ct);
            return streamError;
        }

        var copyResult = await _dbHelperService.TryFunction(
            cancel => fileRequest.CopyToAsync(stream, cancel),
            ServiceResult.FileCreationError, ct: ct);

        var disposeResult = await _dbHelperService.TryFunction(
            async _ => await stream.DisposeAsync(),
            ServiceResult.FileCreationError);

        if (copyResult.Result != ServiceResult.Success)
        {
            await BatchRollback(batchPath, ct);
            return copyResult;
        }

        if (disposeResult.Result != ServiceResult.Success)
        {
            await BatchRollback(batchPath, ct);
            return disposeResult;
        }

        var fileTypesResult = await _cacheService.GetAllFileTypesByMime(ct);

        if (!fileTypesResult.TryPickT0(out var fileTypes, out var cacheError))
        {
            await BatchRollback(batchPath, ct);
            return cacheError;
        }

        string mime = fileRequest.ContentType.Trim().ToLowerInvariant();

        if (!fileTypes.TryGetValue(mime, out var type))
        {
            await BatchRollback(batchPath, ct);
            return ServiceResult.InvalidMime.Return(_logger);
        }

        var file = new Models.File
        {
            URL = destinationPath.Replace('\\', '/'),
            FileTypeId = type.Id
        };

        _context.Files.Add(file);
        return file;
    }

    public ServiceResultContext BatchComplete(string batchPath)
    {
        if (!System.IO.File.Exists(batchPath))
            return ServiceResult.Success.Return(_logger);

        var deletionResult = _dbHelperService.TryFunction(() =>
        {
            System.IO.File.Delete(batchPath);
            return true;
        }, ServiceResult.BatchError);

        return deletionResult.TryPickT0(out _, out var deletionError) ?
            ServiceResult.Success.Return(_logger) : deletionError;
    }

    public async Task<ServiceResultContext> BatchRollback(
        string batchPath, CancellationToken ct)
    {
        _context.ChangeTracker.Clear();

        var contentResult = await _dbHelperService.TryFunction(
            cancel => System.IO.File.ReadAllTextAsync(batchPath, cancel),
            ServiceResult.BatchError, ct: ct);

        if (!contentResult.TryPickT0(out var batchContent, out var contentError))
            return contentError;

        var listResult = _dbHelperService.TryFunction(
            () => JsonSerializer.Deserialize<List<string>>(batchContent) ?? [],
            ServiceResult.BatchError);

        if (!listResult.TryPickT0(out var batchList, out var listError))
            return listError;

        ServiceResultContext? firstError = null;

        var files = batchList
            .Where(file => !string.IsNullOrWhiteSpace(file))
            .Select(file => file.Trim())
            .Where(System.IO.File.Exists);

        foreach (string file in files)
        {
            var deletionResult = _dbHelperService.TryFunction(() =>
            {
                System.IO.File.Delete(file);
                return true;
            }, ServiceResult.FileDeletionError);

            if (!deletionResult.TryPickT0(out _, out var deletionError))
                firstError ??= deletionError;
        }

        if (System.IO.File.Exists(batchPath))
        {
            var batchDeletionResult = _dbHelperService.TryFunction(() =>
            {
                System.IO.File.Delete(batchPath);
                return true;
            }, ServiceResult.BatchError);

            if (!batchDeletionResult.TryPickT0(out _, out var batchDeletionError))
                firstError ??= batchDeletionError;
        }

        return firstError is null ? ServiceResult.Success.Return(_logger) : firstError;
    }

    public bool ValidateContext(FileContext context, IFormFile form)
    {
        string contentType = form.ContentType?.Trim().ToLowerInvariant() ?? "";

        if (string.IsNullOrWhiteSpace(contentType))
            return false;

        return context switch
        {
            FileContext.Avatar =>
                contentType.StartsWith("image/", StringComparison.Ordinal),

            FileContext.Previews =>
                contentType.StartsWith("image/", StringComparison.Ordinal) ||
                contentType.StartsWith("video/", StringComparison.Ordinal),

            FileContext.Resource =>
                contentType.StartsWith("application/", StringComparison.Ordinal),

            _ => false
        };
    }

    public async Task<ServiceResultContext> ValidateFile(
        IFormFile form, CancellationToken ct = default)
    {
        if (form.Length <= 0)
            return ServiceResult.EmptyFile.Return(_logger);

        if (form.Length > MAX_FILE_SIZE)
            return ServiceResult.FileSizeSurpassTheLimit.Return(_logger);

        var expectedMimeResult = await GetExpectedMime(form, ct);

        if (!expectedMimeResult.TryPickT0(out var expectedMime, out var mimeError))
            return mimeError;

        if (string.IsNullOrWhiteSpace(expectedMime))
            return ServiceResult.InvalidMime.Return(_logger);

        bool isImage = expectedMime.StartsWith("image/", StringComparison.Ordinal);
        bool isVideo = expectedMime.StartsWith("video/", StringComparison.Ordinal);

        if ((isImage || isVideo) && form.Length > MAX_MEDIA_FILE_SIZE)
            return ServiceResult.MediaSizeSurpassTheLimit.Return(_logger);

        var streamResult = _dbHelperService.TryFunction(
            form.OpenReadStream, ServiceResult.FormReadingError);

        if (!streamResult.TryPickT0(out var stream, out var streamError))
            return streamError;

        using var streamScope = stream;

        if (!stream.CanRead || !stream.CanSeek || stream.Length != form.Length)
            return ServiceResult.FormReadingError.Return(_logger);

        OneOf<string, ServiceResultContext> inspectionResult;

        if (isImage)
            inspectionResult = InspectImage(stream, ct);
        else if (isVideo)
            inspectionResult = await InspectVideo(stream, ct);
        else
            inspectionResult = InspectArchive(stream, ct);

        if (!inspectionResult.TryPickT0(out var detectedMime, out var inspectionError))
            return inspectionError;

        if (!MimeEquals(detectedMime, expectedMime))
            return ServiceResult.MimeMismatch.Return(_logger);

        return ServiceResult.Success.Return(_logger);
    }

    public async Task<OneOf<T, ServiceResultContext>> TrySyncContextWithFiles<T>(
        string batchPath, T success, CancellationToken ct = default)
    {
        var result = await _dbHelperService.TryFunction(async cancel =>
        {
            await _context.SaveChangesAsync(cancel);
            return success;
        }, ServiceResult.DBError,
        () => BatchRollback(batchPath, CancellationToken.None), ct: ct);

        if (result.IsT0)
            _ = BatchComplete(batchPath);

        return result;
    }

    public async Task<ServiceResultContext> TrySyncContextWithFiles(
        string batchPath, CancellationToken ct = default)
    {
        var result = await TrySyncContextWithFiles(batchPath, true, ct);

        return result.TryPickT0(out _, out var error) ?
            ServiceResult.Success.Return(_logger) : error;
    }

    private async Task<OneOf<string, ServiceResultContext>> GetExpectedMime(
        IFormFile form, CancellationToken ct = default)
    {
        var fileTypesResult = await _cacheService.GetAllFileTypesByMime(ct);

        if (!fileTypesResult.TryPickT0(out var fileTypes, out var cacheError))
            return cacheError;

        string mime = form.ContentType?.Trim().ToLowerInvariant() ?? "";
        return fileTypes.ContainsKey(mime) ? mime : "";
    }

    private OneOf<string, ServiceResultContext> InspectImage(
        Stream stream, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ServiceResult.RequestCancelled.Return(_logger);

        var codecResult = _dbHelperService.TryFunction(
            () => SKCodec.Create(stream), ServiceResult.InvalidImage);

        if (!codecResult.TryPickT0(out var codec, out var codecError))
            return codecError;

        if (codec is null)
            return ServiceResult.InvalidImage.Return(_logger);

        using var codecScope = codec;

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
            return ServiceResult.InvalidMime.Return(_logger);

        if (codec.FrameCount < 0)
            return ServiceResult.InvalidImageNumberOfFrames.Return(_logger);

        var source = codec.Info;

        if (source.Width <= 0 || source.Height <= 0 ||
            (long)source.Width * source.Height > MAX_IMAGE_PIXELS)
            return ServiceResult.InvalidImageSize.Return(_logger);

        if (codec.FrameCount > MAX_IMAGE_FRAMES)
            return ServiceResult.ImageSurpassFramesLimit.Return(_logger);

        var info = new SKImageInfo(source.Width, source.Height,
            SKColorType.Rgba8888, SKAlphaType.Premul);

        if (ct.IsCancellationRequested)
            return ServiceResult.RequestCancelled.Return(_logger);

        using var bitmap = new SKBitmap(info);
        var pixels = bitmap.GetPixels();

        if (pixels == IntPtr.Zero)
            return ServiceResult.InvalidImageSize.Return(_logger);

        int frameCount = Math.Max(1, codec.FrameCount);

        for (int frame = 0; frame < frameCount; frame++)
        {
            if (ct.IsCancellationRequested)
                return ServiceResult.RequestCancelled.Return(_logger);

            bitmap.Erase(SKColors.Transparent);

            var options = new SKCodecOptions(frame) { PriorFrame = -1 };

            var frameResult = _dbHelperService.TryFunction(
                () => codec.GetPixels(info, pixels, bitmap.RowBytes, options),
                ServiceResult.InvalidImageNumberOfFrames);

            if (!frameResult.TryPickT0(out var decodeResult, out var frameError))
                return frameError;

            if (decodeResult != SKCodecResult.Success)
                return ServiceResult.InvalidImageNumberOfFrames.Return(_logger);
        }

        return mime;
    }

    private async Task<OneOf<string, ServiceResultContext>> InspectVideo(
        Stream stream, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ServiceResult.RequestCancelled.Return(_logger);

        var mediaResult = await _dbHelperService.TryFunction(
            cancel => _mediaAnalyzer.AnalyzeAsync(stream, true, cancel),
            ServiceResult.InvalidVideo, ct: ct);

        if (!mediaResult.TryPickT0(out var media, out var mediaError))
            return mediaError;

        if (!media.Success || !media.HasVideo || !media.VideoStreams.Any())
            return ServiceResult.InvalidVideo.Return(_logger);

        if (media.VideoStreams.Any(video => video.Width <= 0 || video.Height <= 0))
            return ServiceResult.InvalidVideo.Return(_logger);

        string? mime = media.General.Format switch
        {
            "MPEG-4" => "video/mp4",
            "WebM" => "video/webm",
            "Ogg" => "video/ogg",
            _ => null
        };

        return mime is null ? ServiceResult.InvalidMime.Return(_logger) : mime;
    }

    private OneOf<string, ServiceResultContext> InspectArchive(
        Stream stream, CancellationToken ct = default)
    {
        if (ct.IsCancellationRequested)
            return ServiceResult.RequestCancelled.Return(_logger);

        if (!stream.CanRead || !stream.CanSeek)
            return ServiceResult.FormReadingError.Return(_logger);

        if (stream.Length == 0)
            return ServiceResult.EmptyFile.Return(_logger);

        var archiveTypeResult = _dbHelperService.TryFunction(() =>
        {
            bool isArchive = ArchiveFactory.IsArchive(stream, out var archiveType);
            return (IsArchive: isArchive, Type: archiveType);
        }, ServiceResult.InvalidFile);

        if (!archiveTypeResult.TryPickT0(out var archiveInfo, out var archiveTypeError))
            return archiveTypeError;

        if (!archiveInfo.IsArchive)
            return ServiceResult.InvalidFile.Return(_logger);

        string? mime = archiveInfo.Type switch
        {
            ArchiveType.Zip => "application/zip",
            ArchiveType.Rar => "application/vnd.rar",
            ArchiveType.SevenZip => "application/x-7z-compressed",
            ArchiveType.Tar => "application/x-tar",
            ArchiveType.GZip => "application/gzip",
            _ => null
        };

        if (mime is null)
            return ServiceResult.InvalidMime.Return(_logger);

        if (ct.IsCancellationRequested)
            return ServiceResult.RequestCancelled.Return(_logger);

        stream.Position = 0;

        var archiveResult = _dbHelperService.TryFunction(
            () => ArchiveFactory.OpenArchive(stream, new ReaderOptions
            {
                LeaveStreamOpen = true,
                LookForHeader = false
            }),
            ServiceResult.FormReadingError);

        if (!archiveResult.TryPickT0(out var archive, out var archiveError))
            return archiveError;

        using var archiveScope = archive;

        if (archive.Type != archiveInfo.Type)
            return ServiceResult.InvalidFile.Return(_logger);

        var entriesResult = _dbHelperService.TryFunction(
            () => archive.Entries.Take(MAX_ARCHIVE_ENTRIES + 1)
                .TakeWhile(_ => !ct.IsCancellationRequested)
                .ToArray(),
            ServiceResult.InvalidFile);

        if (!entriesResult.TryPickT0(out var entries, out var entriesError))
            return entriesError;

        if (ct.IsCancellationRequested)
            return ServiceResult.RequestCancelled.Return(_logger);

        if (entries.Length > MAX_ARCHIVE_ENTRIES ||
            entries.Any(entry => entry.IsEncrypted || !entry.IsComplete || entry.IsSplitAfter))
            return ServiceResult.InvalidFile.Return(_logger);

        return entries.Any(entry => !entry.IsDirectory) ?
            mime : ServiceResult.InvalidFile.Return(_logger);
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

    private async Task<ServiceResultContext> UpdateBatch(
        string physicalPath, string batchPath, CancellationToken ct)
    {
        var contentResult = await _dbHelperService.TryFunction(
            cancel => System.IO.File.ReadAllTextAsync(batchPath, cancel),
            ServiceResult.UpdateBatchError, ct: ct);

        if (!contentResult.TryPickT0(out var batchContent, out var contentError))
            return contentError;

        var listResult = _dbHelperService.TryFunction(
            () => JsonSerializer.Deserialize<List<string>>(batchContent) ?? [],
            ServiceResult.UpdateBatchError);

        if (!listResult.TryPickT0(out var batchList, out var listError))
            return listError;

        batchList.Add(physicalPath);
        string serializedBatch = JsonSerializer.Serialize(batchList);

        return await _dbHelperService.TryFunction(
            cancel => System.IO.File.WriteAllTextAsync(
                batchPath, serializedBatch, cancel),
            ServiceResult.UpdateBatchError, ct: ct);
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
