using OneOf;
using SNEStorage.DTOs;
using System.Globalization;

namespace SNEStorage.Services;

public class ToolUsageService(
    IWebHostEnvironment environment, IConfiguration configuration,
    ILogger<ToolUsageService> logger)
{
    private readonly string _directory = Path.GetFullPath(
        configuration["ToolUsage:Directory"] ?? Path.Combine("Storage", "ToolUsage"),
        environment.ContentRootPath);
    private readonly ILogger<ToolUsageService> _logger = logger;

    public Task<OneOf<long, ServiceResultContext>> Get(CancellationToken ct = default)
    {
        return ReadOrIncrement(false, ct);
    }

    public Task<OneOf<long, ServiceResultContext>> RegisterUse(CancellationToken ct = default)
    {
        return ReadOrIncrement(true, ct);
    }

    private async Task<OneOf<long, ServiceResultContext>> ReadOrIncrement(
        bool increment, CancellationToken ct)
    {
        try
        {
            Directory.CreateDirectory(_directory);
            await using var lease = await AcquireLock(ct);
            long count = await ReadCount(ct);
            if (!increment)
                return count;

            if (count == long.MaxValue)
                return ServiceResult.Unavailable.Return(_logger);

            long next = count + 1;
            string pending = Path.Combine(_directory, "apply-ips-bps.pending");
            await File.WriteAllTextAsync(pending, next.ToString(CultureInfo.InvariantCulture), ct);
            File.Move(pending, Path.Combine(_directory, "apply-ips-bps.txt"), true);
            return next;
        }
        catch (OperationCanceledException exception) when (ct.IsCancellationRequested)
        {
            return ServiceResult.RequestCancelled.Return(_logger, exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return ServiceResult.Unavailable.Return(_logger, exception);
        }
    }

    private async Task<long> ReadCount(CancellationToken ct)
    {
        string text;
        try
        {
            text = await File.ReadAllTextAsync(Path.Combine(_directory, "apply-ips-bps.txt"), ct);
        }
        catch (FileNotFoundException)
        {
            return 0;
        }

        return long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture,
            out long count) && count >= 0 ? count :
            throw new InvalidDataException("The tool usage counter is invalid; it was not reset.");
    }

    private async Task<FileStream> AcquireLock(CancellationToken ct)
    {
        string path = Path.Combine(_directory, "apply-ips-bps.lock");
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                // Exclusive access also protects overlapping app processes during a restart.
                return new FileStream(path, FileMode.OpenOrCreate,
                    FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException exception) when (attempt < 49 &&
                (exception.HResult & 0xFFFF) is 32 or 33)
            {
                await Task.Delay(20, ct);
            }
        }
    }
}
