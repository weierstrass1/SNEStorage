using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using OneOf;
using SNEStorage.DTOs;
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
    TimeZone,
    Reasons
}

public class CacheService(IMemoryCache cache, IServiceScopeFactory factory) : IDisposable
{
    private readonly IMemoryCache _cache = cache;
    private readonly IServiceScopeFactory _factory = factory;
    private static readonly TimeSpan _lifetime = TimeSpan.FromHours(24);
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    private delegate Task<OneOf<FrozenDictionary<TKey, TValue>, ServiceResultContext>>
        CacheLoader<TKey, TValue>(AsyncServiceScope scope, DBHelperService helper,
            CancellationToken ct) where TKey : notnull;
    public Task<OneOf<Dictionary<long, Reason>, ServiceResultContext>>
    GetAllReasons(CancellationToken ct = default)
    {
        return Get(CacheKeys.Reasons, LoadReasons, ct);
    }

    public Task<OneOf<Dictionary<long, Models.TimeZone>, ServiceResultContext>>
        GetAllTimeZones(CancellationToken ct = default)
    {
        return Get(CacheKeys.TimeZone, LoadTimeZones, ct);
    }

    public Task<OneOf<Dictionary<long, ResourceType>, ServiceResultContext>>
        GetAllTypes(CancellationToken ct = default)
    {
        return Get(CacheKeys.Type, LoadTypes, ct);
    }

    public Task<OneOf<Dictionary<long, ResourceCategory>, ServiceResultContext>>
        GetAllCategories(CancellationToken ct = default)
    {
        return Get(CacheKeys.Category, LoadCategories, ct);
    }

    public Task<OneOf<Dictionary<long, Visibility>, ServiceResultContext>>
        GetAllVisibilities(CancellationToken ct = default)
    {
        return Get(CacheKeys.Visibility, LoadVisibilities, ct);
    }

    public Task<OneOf<Dictionary<string, FileType>, ServiceResultContext>>
        GetAllFileTypesByMime(CancellationToken ct = default)
    {
        return Get(CacheKeys.FileTypeByMime, LoadFileTypesByMime, ct);
    }

    public Task<OneOf<Dictionary<long, FileType>, ServiceResultContext>>
        GetAllFileTypes(CancellationToken ct = default)
    {
        return Get(CacheKeys.FileType, LoadFileTypes, ct);
    }

    private async Task<OneOf<Dictionary<TKey, TValue>, ServiceResultContext>> Get<TKey, TValue>(
        CacheKeys key, CacheLoader<TKey, TValue> loader, CancellationToken ct = default)
        where TKey : notnull
    {
        if (_cache.TryGetValue(key, out FrozenDictionary<TKey, TValue>? dictionary) &&
            dictionary is not null)
            return dictionary.ToDictionary();

        await using var scope = _factory.CreateAsyncScope();
        var helper = scope.ServiceProvider.GetRequiredService<DBHelperService>();

        var waitResult = await helper.TryFunction(
            cancel => _loadGate.WaitAsync(cancel),
            ServiceResult.CacheError, ct: ct);

        if (waitResult.Result != ServiceResult.Success)
            return waitResult;

        try
        {
            if (_cache.TryGetValue(key, out dictionary) && dictionary is not null)
                return dictionary.ToDictionary();

            var loadResult = await loader(scope, helper, ct);

            if (!loadResult.TryPickT0(out var loadedDictionary, out var loadError))
                return loadError;

            return loadedDictionary.ToDictionary();
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private Task<OneOf<FrozenDictionary<long, ResourceType>, ServiceResultContext>> LoadTypes(
        AsyncServiceScope scope, DBHelperService helper, CancellationToken ct)
    {
        var context = scope.ServiceProvider.GetRequiredService<SnestorageContext>();

        return Load(CacheKeys.Type, value => value.Id,
            context.ResourceTypes.AsNoTracking()
                .Include(type => type.Category)
                .ToListAsync,
            helper, ct);
    }

    private Task<OneOf<FrozenDictionary<long, ResourceCategory>, ServiceResultContext>>
        LoadCategories(AsyncServiceScope scope, DBHelperService helper, CancellationToken ct)
    {
        var context = scope.ServiceProvider.GetRequiredService<SnestorageContext>();

        return Load(CacheKeys.Category, value => value.Id,
            context.ResourceCategories.AsNoTracking().ToListAsync, helper, ct);
    }
    private Task<OneOf<FrozenDictionary<long, Reason>, ServiceResultContext>>
    LoadReasons(AsyncServiceScope scope, DBHelperService helper, CancellationToken ct)
    {
        var context = scope.ServiceProvider.GetRequiredService<SnestorageContext>();

        return Load(CacheKeys.Reasons, value => value.Id,
            context.Reasons.AsNoTracking().Include(r => r.ReasonCategory).ToListAsync, helper, ct);
    }

    private Task<OneOf<FrozenDictionary<long, Models.TimeZone>, ServiceResultContext>>
        LoadTimeZones(AsyncServiceScope scope, DBHelperService helper, CancellationToken ct)
    {
        var context = scope.ServiceProvider.GetRequiredService<SnestorageContext>();

        return Load(CacheKeys.TimeZone, value => value.Id,
            context.TimeZones.AsNoTracking().ToListAsync, helper, ct);
    }

    private Task<OneOf<FrozenDictionary<long, Visibility>, ServiceResultContext>>
        LoadVisibilities(AsyncServiceScope scope, DBHelperService helper, CancellationToken ct)
    {
        var context = scope.ServiceProvider.GetRequiredService<SnestorageContext>();

        return Load(CacheKeys.Visibility, value => value.Id,
            context.Visibilities.AsNoTracking().ToListAsync, helper, ct);
    }

    private Task<OneOf<FrozenDictionary<string, FileType>, ServiceResultContext>>
        LoadFileTypesByMime(AsyncServiceScope scope, DBHelperService helper, CancellationToken ct)
    {
        var context = scope.ServiceProvider.GetRequiredService<SnestorageContext>();

        return Load(CacheKeys.FileTypeByMime, value => value.Value,
            context.FileTypes.AsNoTracking().ToListAsync, helper, ct);
    }

    private Task<OneOf<FrozenDictionary<long, FileType>, ServiceResultContext>> LoadFileTypes(
        AsyncServiceScope scope, DBHelperService helper, CancellationToken ct)
    {
        var context = scope.ServiceProvider.GetRequiredService<SnestorageContext>();

        return Load(CacheKeys.FileType, value => value.Id,
            context.FileTypes.AsNoTracking().ToListAsync, helper, ct);
    }

    private async Task<OneOf<FrozenDictionary<TKey, TValue>, ServiceResultContext>>
        Load<TKey, TValue>(CacheKeys cacheKey, Func<TValue, TKey> keySelector,
            Func<CancellationToken, Task<List<TValue>>> getter,
            DBHelperService helper, CancellationToken ct = default)
        where TKey : notnull
    {
        var valuesResult = await helper.TryFunction(
            getter, ServiceResult.CacheError, ct: ct);

        if (!valuesResult.TryPickT0(out var values, out var queryError))
            return queryError;

        var dictionaryResult = helper.TryFunction(
            () => values.ToFrozenDictionary(keySelector, value => value),
            ServiceResult.CacheError);

        if (!dictionaryResult.TryPickT0(out var dictionary, out var dictionaryError))
            return dictionaryError;

        _cache.Set(cacheKey, dictionary, _lifetime);
        return dictionary;
    }

    public void Dispose()
    {
        _loadGate.Dispose();
        GC.SuppressFinalize(this);
    }
}
