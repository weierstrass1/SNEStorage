using OneOf;
using SNEStorage.Models;

namespace SNEStorage.Services;
public class DBHelperService(SnestorageContext context)
{
    private readonly SnestorageContext _context = context;
    public async Task<OneOf<T1,T2>> TrySyncContext<T1, T2>(T1 res, T2 error, CancellationToken ct = default)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
            return res;
        }
        catch
        {
            _context.ChangeTracker.Clear();
            return error;
        }
    }
    public async Task<bool> TrySyncContext(CancellationToken ct = default)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
            return true;
        }
        catch
        {
            _context.ChangeTracker.Clear();
            return false;
        }
    }
    public async Task<OneOf<T1, T2>> TryFunction<T1, T2>(Func<CancellationToken, Task<T1>> func, T2 error, Func<Task>? ErrorCallback = null, CancellationToken ct = default)
    {
        try
        {
            return await func(ct);
        }
        catch
        {
            if(ErrorCallback != null)
                await ErrorCallback();
            return error;
        }
    }
    public T? TryFunction<T>(Func<T> func)
    {
        try
        {
            return func();
        }
        catch
        {
            return default;
        }
    }
    public async Task<T1?> TryFunction<T1>(Func<CancellationToken, Task<T1>> func, 
        Func<Task>? ErrorCallback = null, CancellationToken ct = default)
    {
        try
        {
            return await func(ct);
        }
        catch
        {
            if (ErrorCallback != null)
                await ErrorCallback();
            return default;
        }
    }
    public async Task<bool> TryFunction(Func<CancellationToken, Task> func,
        Func<Task>? ErrorCallback = null, CancellationToken ct = default)
    {
        try
        {
            await func(ct);
            return true;
        }
        catch
        {
            if (ErrorCallback != null)
                await ErrorCallback();
            return false;
        }
    }
    public string? GetEditString(string? newValue, string? oldValue)
    {
        if (newValue is null)
            return oldValue;
        if (string.IsNullOrWhiteSpace(newValue))
            return "";
        return newValue;
    }
}
