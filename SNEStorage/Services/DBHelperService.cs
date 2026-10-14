using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;
using System.Runtime.CompilerServices;

namespace SNEStorage.Services;

public class DBHelperService(
    SnestorageContext context,
    ILogger<DBHelperService> logger)
{
    private readonly SnestorageContext _context = context;
    private readonly ILogger<DBHelperService> _logger = logger;
    public async Task<OneOf<T, ServiceResultContext>> TryFunction<T>(
        Func<CancellationToken, Task<T>> function, ServiceResult? failure = null,
        Func<Task>? errorCallback = null, [CallerFilePath] string filePath = "", 
        [CallerMemberName] string methodName = "", [CallerLineNumber] int line = 0,
        CancellationToken ct = default)
    {
        try
        {
            return await function(ct);
        }
        catch (Exception exception)
        {
            await RunErrorCallback(errorCallback, filePath, methodName, line);

            return CreateFailure(failure ?? ServiceResult.DBError, exception, filePath, 
                methodName, line, ct);
        }
    }
    public OneOf<T, ServiceResultContext> TryFunction<T>(Func<T> function, ServiceResult? failure = null,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "", 
        [CallerLineNumber] int line = 0)
    {
        try
        {
            return function();
        }
        catch (Exception exception)
        {
            CancellationToken ct = exception is OperationCanceledException cancellation ?
                    cancellation.CancellationToken : default;

            return CreateFailure(failure ?? ServiceResult.DBError, exception, filePath, 
                methodName, line, ct);
        }
    }
    public async Task<ServiceResultContext> TryFunction(
        Func<CancellationToken, Task> function, ServiceResult? failure = null,
        Func<Task>? errorCallback = null, [CallerFilePath] string filePath = "",
        [CallerMemberName] string methodName = "", [CallerLineNumber] int line = 0, 
        CancellationToken ct = default)
    {
        var result = await TryFunction(
            async cancel =>
            {
                await function(cancel);
                return true;
            },
            failure, errorCallback, filePath, methodName, line, ct);

        return result.TryPickT0(out _, out var error) ?
            ServiceResult.Success.Return(
                _logger, null, filePath, methodName, line) : error;
    }
    public Task<OneOf<T, ServiceResultContext>> TrySyncContext<T>(T success,
        CancellationToken ct = default, [CallerFilePath] string filePath = "",
        [CallerMemberName] string methodName = "", [CallerLineNumber] int line = 0)
    {
        return TryFunction(
            async cancel =>
            {
                await _context.SaveChangesAsync(cancel);
                return success;
            },
            ServiceResult.DBError, ClearChangeTracker, filePath, methodName, line, ct);
    }
    public async Task<ServiceResultContext> TrySyncContext(CancellationToken ct = default,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "", 
        [CallerLineNumber] int line = 0)
    {
        var result = await TrySyncContext(true, ct, filePath, methodName, line);

        return result.TryPickT0(out _, out var error) ?
            ServiceResult.Success.Return(_logger, null, filePath, methodName, line) : error;
    }

    private ServiceResultContext CreateFailure(ServiceResult failure, Exception exception,
        string filePath, string methodName, int line, CancellationToken ct)
    {
        ServiceResult result = exception switch
        {
            OperationCanceledException when ct.IsCancellationRequested =>
                ServiceResult.RequestCancelled,
            OperationCanceledException =>
                ServiceResult.CancellationTokenError,
            _ => failure
        };

        return result.Return(_logger, exception, filePath, methodName, line);
    }
    private async Task RunErrorCallback(Func<Task>? errorCallback, string filePath,
        string methodName, int line)
    {
        if (errorCallback is null)
            return;

        try
        {
            await errorCallback();
        }
        catch (Exception exception)
        {
            ServiceResult.Error.Return(_logger, exception, filePath, methodName, line);
        }
    }
    private Task ClearChangeTracker()
    {
        _context.ChangeTracker.Clear();
        return Task.CompletedTask;
    }
    public string? GetEditString(string? newValue, string? oldValue)
    {
        if (newValue is null)
            return oldValue;

        return string.IsNullOrWhiteSpace(newValue) ?
            "" : newValue;
    }
}