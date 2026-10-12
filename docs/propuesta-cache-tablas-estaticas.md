# Caché de tablas estáticas

Propuesta para aplicar manualmente, 2026-10-10. No se modificó backend ni se ejecutaron pruebas o compilación.

## Qué se guarda

FileType, Visibility, ResourceCategory, ResourceType y TimeZone. Videogames queda fuera.

Una entrada de IMemoryCache almacena un catálogo de valores inmutables. La primera consulta carga las cinco tablas; las siguientes leen los diccionarios en RAM. Una expiración de 24 horas es una propuesta ajustable. Reiniciar el proceso también pierde la caché y provoca una nueva carga.

Solo se guardan campos escalares. No se guardan entidades con seguimiento ni sus colecciones de navegación. FrozenDictionary protege la estructura y los records siguientes protegen sus valores. Un SemaphoreSlim compartido evita que varias solicitudes carguen las tablas a la vez cuando falta la entrada.

La caché pertenece a cada instancia del proceso. Si hubiera varias instancias, cada una tendría su catálogo y su invalidación.

## 1. Servicio propuesto

Crear Services/StaticDataCacheService.cs con este contenido:

```csharp
using System.Collections.Frozen;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using SNEStorage.Models;

namespace SNEStorage.Services;

public sealed record CachedFileType(long Id, string Name, string Value);
public sealed record CachedVisibility(long Id, string? Name, bool ResourceOnly);
public sealed record CachedCategory(long Id, string Name);
public sealed record CachedResourceType(long Id, string? Name, long CategoryId);
public sealed record CachedTimeZone(long Id, string Name, int Value);

public sealed record StaticDataCatalog(
    FrozenDictionary<long, CachedFileType> FileTypes,
    FrozenDictionary<string, CachedFileType> FileTypesByMime,
    FrozenDictionary<long, CachedVisibility> Visibilities,
    FrozenDictionary<long, CachedCategory> Categories,
    FrozenDictionary<long, CachedResourceType> ResourceTypes,
    FrozenDictionary<long, CachedTimeZone> TimeZones);

public sealed class StaticDataCacheService(
    IMemoryCache memoryCache,
    IServiceScopeFactory scopeFactory) : IDisposable
{
    private const string CacheKey = "snestorage:static-data:v1";
    private static readonly TimeSpan Lifetime = TimeSpan.FromHours(24);
    private readonly SemaphoreSlim _loadGate = new(1, 1);

    public async Task<StaticDataCatalog> GetAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        if (memoryCache.TryGetValue<StaticDataCatalog>(CacheKey, out var cached)
            && cached is not null)
            return cached;

        await _loadGate.WaitAsync(ct);
        try
        {
            if (memoryCache.TryGetValue<StaticDataCatalog>(CacheKey, out cached)
                && cached is not null)
                return cached;

            var catalog = await LoadAsync(ct);
            memoryCache.Set(CacheKey, catalog, Lifetime);
            return catalog;
        }
        finally
        {
            _loadGate.Release();
        }
    }

    private async Task<StaticDataCatalog> LoadAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SnestorageContext>();

        // Secuenciales: EF no permite queries simultáneas en el mismo DbContext.
        var fileTypes = await context.FileTypes.AsNoTracking()
            .Select(t => new CachedFileType(t.Id, t.Name, t.Value))
            .ToListAsync(ct);

        var visibilities = await context.Visibilities.AsNoTracking()
            .Select(v => new CachedVisibility(v.Id, v.Name, v.ResourceOnly))
            .ToListAsync(ct);

        var categories = await context.ResourceCategories.AsNoTracking()
            .Select(c => new CachedCategory(c.Id, c.Name))
            .ToListAsync(ct);

        var resourceTypes = await context.ResourceTypes.AsNoTracking()
            .Select(t => new CachedResourceType(t.Id, t.Name, t.CategoryId))
            .ToListAsync(ct);

        var timeZones = await context.TimeZones.AsNoTracking()
            .Select(t => new CachedTimeZone(t.Id, t.Name, t.Value))
            .ToListAsync(ct);

        return new StaticDataCatalog(
            fileTypes.ToFrozenDictionary(t => t.Id),
            fileTypes.ToFrozenDictionary(
                t => t.Value.Trim(), t => t, StringComparer.OrdinalIgnoreCase),
            visibilities.ToFrozenDictionary(v => v.Id),
            categories.ToFrozenDictionary(c => c.Id),
            resourceTypes.ToFrozenDictionary(t => t.Id),
            timeZones.ToFrozenDictionary(t => t.Id));
    }

    public async Task<FileType?> GetFileTypeAsync(
        string mime, CancellationToken ct = default)
    {
        var catalog = await GetAsync(ct);

        if (!catalog.FileTypesByMime.TryGetValue(mime.Trim(), out var item))
            return null;

        // Copia independiente: no entregar una entidad mutable compartida.
        return new FileType
        {
            Id = item.Id,
            Name = item.Name,
            Value = item.Value
        };
    }

    public async Task InvalidateAsync(CancellationToken ct = default)
    {
        // Evita invalidar mientras una carga anterior publica su resultado.
        await _loadGate.WaitAsync(ct);
        try
        {
            memoryCache.Remove(CacheKey);
        }
        finally
        {
            _loadGate.Release();
        }
    }

    public void Dispose() => _loadGate.Dispose();
}
```

La creación del diccionario por MIME exige valores únicos ignorando mayúsculas y espacios exteriores. Si hay dos filas con el mismo MIME normalizado, la carga falla: corregir esos duplicados en lugar de elegir una fila arbitrariamente. MIME diferentes como application/zip y application/x-zip-compressed son claves distintas.

Si una consulta falla, GetAsync propaga el error y no guarda un catálogo incompleto o una lista vacía como resultado exitoso. El servicio/controlador consumidor puede manejarlo con el patrón de errores del proyecto. La siguiente solicitud vuelve a intentar cargar.

## 2. Registro en Program.cs

Antes de builder.Build():

```csharp
services.AddMemoryCache();
services.AddSingleton<StaticDataCacheService>();
```

No cambiar el lifetime de SnestorageContext ni hacerlo singleton. El servicio crea un scope temporal solo cuando necesita cargar las tablas. No requiere un paquete adicional en este proyecto ASP.NET Core.

La implementación es de carga bajo demanda. Si quieres precargar antes de aceptar solicitudes, añadir antes de app.Run():

```csharp
await app.Services.GetRequiredService<StaticDataCacheService>().GetAsync();
```

Esta variante retrasa el arranque mientras carga y hace que un error de carga impida arrancar. Es opcional.

## 3. FileService

Inyectar StaticDataCacheService en su constructor. Sustituir la query de FileTypes por:

```csharp
FileType? type = await staticDataCache.GetFileTypeAsync(detectedMime, ct);
```

detectedMime debe proceder de la inspección del contenido. Durante la transición puedes consultar un MIME declarado, pero eso no es validación del archivo.

Al crear Models.File, usar el ID de la fila existente:

```csharp
var file = new Models.File
{
    URL = destinationPath.Replace('\\', '/'),
    FileTypeId = type.Id
};
```

No hacer context.FileTypes.Add(type). Tampoco asignar esta copia nueva a file.FileType dentro de un grafo añadido: EF podría intentar insertar otra fila. Usar FileTypeId evita ese problema.

La detección del contenido puede mantenerse como helper puro. Si quieres que ValidateFile también resuelva FileType mediante esta caché, conviene un método de instancia async que devuelva Task<FileType?>, porque una caché vacía necesita una consulta async. Evitar .Result o .GetAwaiter().GetResult() para mantener una firma síncrona.

## 4. RegisterController.GetRegistrationOptions

Inyectar StaticDataCacheService y sustituir las consultas de opciones por:

```csharp
var catalog = await staticDataCache.GetAsync(ct);

var options = new RegistrationOptionsDto
{
    TimeZones = catalog.TimeZones.Values
        .OrderBy(t => t.Id)
        .Select(t => new SelectOptionDto { Id = t.Id, Name = t.Name })
        .ToList(),
    EmailVisibilities = catalog.Visibilities.Values
        .Where(v => !v.ResourceOnly)
        .OrderBy(v => v.Id)
        .Select(v => new SelectOptionDto { Id = v.Id, Name = v.Name })
        .ToList()
};

return Ok(options);
```

Estas operaciones LINQ son en memoria; no se convierten a SQL. Se conserva ResourceOnly y el contrato de respuesta del frontend.

## 5. ResourceController.GetCreateOptions

Inyectar StaticDataCacheService y sustituir las consultas de opciones por:

```csharp
var catalog = await staticDataCache.GetAsync(ct);

var options = new ResourceCreateOptionsDto
{
    ResourceTypes = catalog.ResourceTypes.Values
        .OrderBy(t => t.Name)
        .Select(t => new ResourceTypeDTO
        {
            Id = t.Id,
            Name = t.Name ?? string.Empty,
            CategoryId = t.CategoryId,
            CategoryName = catalog.Categories[t.CategoryId].Name
        })
        .ToList(),
    Visibilities = catalog.Visibilities.Values
        .OrderBy(v => v.Id)
        .Select(v => new SelectOptionDto { Id = v.Id, Name = v.Name })
        .ToList()
};

return Ok(options);
```

Se mantiene CategoryId/CategoryName para el picker. El código presupone que cada ResourceType tiene una categoría existente, coherente con la foreign key.

## 6. ResourceService.GetTypes

Inyectar StaticDataCacheService. La comprobación de existencia ya no requiere una query por tipo:

```csharp
private async Task<List<ResourceResourceTypes?>> GetTypes(
    long[] typeIds, CancellationToken ct)
{
    var catalog = await _staticDataCache.GetAsync(ct);
    List<ResourceResourceTypes?> types = [];

    foreach (long typeId in typeIds)
    {
        types.Add(catalog.ResourceTypes.ContainsKey(typeId)
            ? new ResourceResourceTypes
            {
                Id = 0,
                ResourceId = 0,
                ResourceTypeId = typeId
            }
            : null);
    }

    return types;
}
```

Aquí se conserva el comportamiento actual de devolver null para IDs inexistentes; el consumidor decide el error. Usar el mismo catálogo para validar TimeZoneId y EmailVisibilityId en AccountService. No elimina las foreign keys: la DB sigue siendo la autoridad al guardar.

## 7. Refrescar después de cambiar una tabla

Después de guardar y confirmar una modificación en cualquiera de las cinco tablas:

```csharp
await staticDataCache.InvalidateAsync(ct);
```

La próxima lectura recarga las cinco. Para recargar inmediatamente:

```csharp
await staticDataCache.InvalidateAsync(ct);
await staticDataCache.GetAsync(ct);
```

Si editas directamente con SQL Server, la aplicación no recibe una notificación automática: reiniciar el sitio, ejecutar una recarga administrativa protegida o esperar la expiración. No crear un endpoint público de invalidación. Una solicitud que ya recibió un catálogo puede terminar con ese snapshot; la invalidación afecta a las lecturas siguientes.

## Fuentes

- [IMemoryCache en ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/memory?view=aspnetcore-10.0).
- [Consultas sin seguimiento de EF Core](https://learn.microsoft.com/en-us/ef/core/querying/tracking).
- [Lifetime de servicios y scopes](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection/overview).
