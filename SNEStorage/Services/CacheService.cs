using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SNEStorage.Models;
using System.Collections.Frozen;

namespace SNEStorage.Services;
public enum CacheKeys
{
    Invalid,
    FileType,
    FileTypeByMime,
    Visibility,
    Category,
    Type,
    TimeZone
}
public class CacheService(IMemoryCache cache,
    IServiceScopeFactory factory) : IDisposable
{
    private readonly IMemoryCache _cache = cache;
    private readonly IServiceScopeFactory _factory = factory;
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);
    private readonly SemaphoreSlim _loadGate = new(1, 1);
    public async Task<Dictionary<long, Models.TimeZone>?> GetAllTimeZones(CancellationToken ct = default)
    {
        return await Get<long, Models.TimeZone>(CacheKeys.TimeZone, ct);
    }
    public async Task<Dictionary<long, ResourceType>?> GetAllTypes(CancellationToken ct = default)
    {
        return await Get<long, ResourceType>(CacheKeys.Type, ct);
    }
    public async Task<Dictionary<long, ResourceCategory>?> GetAllCategories(CancellationToken ct = default)
    {
        return await Get<long, ResourceCategory>(CacheKeys.Category, ct);
    }
    public async Task<Dictionary<long, Visibility>?> GetAllVisibilities(CancellationToken ct = default)
    {
        return await Get<long, Visibility>(CacheKeys.Visibility, ct);
    }
    public async Task<Dictionary<string, FileType>?> GetAllFileTypesByMime(CancellationToken ct = default)
    {
        return await Get<string, FileType>(CacheKeys.FileTypeByMime, ct);
    }
    public async Task<Dictionary<long, FileType>?> GetAllFileTypes(CancellationToken ct = default)
    {
        return await Get<long, FileType>(CacheKeys.FileType, ct);
    }
    private async Task<Dictionary<T1,T2>?> Get<T1,T2>(CacheKeys key, 
        CancellationToken ct = default) where T1 : notnull
    {
        if (_cache.TryGetValue(key, out var dictionary))
            return ((FrozenDictionary<T1,T2>)dictionary!).ToDictionary();

        await _loadGate.WaitAsync(ct);

        try
        {
            if (_cache.TryGetValue(key, out dictionary))
                return ((FrozenDictionary<T1, T2>)dictionary!).ToDictionary();

            if (!await Load(key, ct))
                return null;

            return ((FrozenDictionary<T1, T2>)_cache.Get(key)!).ToDictionary();
        }
        finally
        {
            _loadGate.Release();
        }
    }
    private async Task<bool> Load(CacheKeys key, CancellationToken ct = default)
    {
        return key switch
        {
            CacheKeys.Category => await LoadCategories(ct),
            CacheKeys.FileType => await LoadFileTypes(ct),
            CacheKeys.FileTypeByMime => await LoadFileTypesByMime(ct),
            CacheKeys.TimeZone => await LoadTimeZones(ct),
            CacheKeys.Type => await LoadTypes(ct),
            CacheKeys.Visibility => await LoadVisibilities(ct),
            _ => false
        };
    }
    private async Task<bool> LoadTypes(CancellationToken ct = default)
    {
        await using var scope = _factory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SnestorageContext>();
        return await Load(CacheKeys.Type, v => v.Id,
            context.ResourceTypes
                .AsNoTracking()
                .Include(t => t.Category)
                .ToListAsync, scope, ct);
    }
    private async Task<bool> LoadCategories(CancellationToken ct = default)
    {
        await using var scope = _factory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SnestorageContext>();
        return await Load(CacheKeys.Category, v => v.Id,
            context.ResourceCategories
                .AsNoTracking()
                .ToListAsync, scope, ct);
    }
    private async Task<bool> LoadTimeZones(CancellationToken ct = default)
    {
        await using var scope = _factory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SnestorageContext>();
        return await Load(CacheKeys.TimeZone, v => v.Id,
            context.TimeZones
                .AsNoTracking()
                .ToListAsync, scope, ct);
    }
    private async Task<bool> LoadVisibilities(CancellationToken ct = default)
    {
        await using var scope = _factory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SnestorageContext>();
        return await Load(CacheKeys.Visibility, v => v.Id,
            context.Visibilities
                .AsNoTracking()
                .ToListAsync, scope, ct);
    }
    private async Task<bool> LoadFileTypesByMime(CancellationToken ct = default)
    {
        await using var scope = _factory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SnestorageContext>();
        return await Load(CacheKeys.FileTypeByMime, v => v.Value,
            context.FileTypes
                .AsNoTracking()
                .ToListAsync, scope, ct);
    }
    private async Task<bool> LoadFileTypes(CancellationToken ct = default)
    {
        await using var scope = _factory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SnestorageContext>();
        return await Load(CacheKeys.FileType, v => v.Id,
            context.FileTypes
                .AsNoTracking()
                .ToListAsync, scope, ct);
    }
    private async Task<bool> Load<T1, T2>(CacheKeys cacheKey,
        Func<T2, T1> keyFunc, Func<CancellationToken, Task<List<T2>>> getter,
        AsyncServiceScope scope, CancellationToken ct = default) where T1 : notnull
    {
        var helper = scope.ServiceProvider.GetRequiredService<DBHelperService>();

        var res = await helper.TryFunction(
            async cancel => await getter(cancel), ct: ct);
        if (res == null)
            return false;

        FrozenDictionary<T1, T2> dictionary = res.ToFrozenDictionary(v => keyFunc(v),
            v => v);

        _cache.Set(cacheKey, dictionary, Lifetime);
        return true;
    }
    public void Dispose()
    {
        _loadGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
