# Propuesta: Get de ResourceService con DTO

Fecha: 10 de octubre de 2026. Propuesta basada en el código actual; no se modificó backend ni frontend.

## Qué cambiaría

1. Get devuelve un ResourceReadDTO proyectado desde SQL, sin Include ni entidades anidadas.
2. La consulta interna de Edit conserva entidades con seguimiento en un método privado GetForEdit. Se elimina el parámetro tracking de Get.
3. Se reutilizan GetAccess, CheckViewAccess, CheckEditAccess, DBHelperService e ItemOrError existentes.
4. Versiones, rating, autores, previews y dependencias siguen llegando por sus acciones actuales.

Varios Includes del Get actual solo se ejecutan con tracking=true. El GET público ya tiene menos relaciones; esta propuesta reduce las columnas obtenidas, las entidades materializadas y el JSON, pero no garantiza por sí sola menos viajes a la DB.

## 1. DTOs de lectura

Estos DTOs se pueden poner en un archivo nuevo dentro de DTOs. No son modelos de EF ni requieren una migración.

```csharp
namespace SNEStorage.DTOs;

public sealed class ResourceReadDTO
{
    public long Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public string? RepositoryURL { get; set; }
    public bool AlreadyModerated { get; set; }
    public bool CanEdit { get; set; }

    public bool IncludesPorn { get; set; }
    public bool IncludesGore { get; set; }
    public bool IncludesPolitics { get; set; }
    public bool IncludesSlurs { get; set; }
    public bool IncludesSensitiveContent { get; set; }

    public required IdNamePair Videogame { get; set; }
    public required IdNamePair Visibility { get; set; }
    public List<ResourceTypeReadDTO> Types { get; set; } = [];
    public List<ResourceTagReadDTO> Tags { get; set; } = [];
}

public sealed record ResourceTypeReadDTO(
    long Id,
    long ResourceTypeId,
    string Name,
    long CategoryId,
    string CategoryName);

public sealed record ResourceTagReadDTO(
    long Id,
    long TagId,
    string Name);
```

En Types, Id es el ID de ResourceResourceTypes. En Tags, Id es el ID de ResourceTags. El editor necesita esos IDs para RemoveTagIDs y RemovedTypeIDs; no deben reemplazarse por TagId o ResourceTypeId.

Las categorías se derivan de Types, quitando duplicados por CategoryId. No hace falta repetir una lista de categorías en la respuesta.

No incluí UserSummary del submitter porque la vista actual no lo muestra. CanEdit permite mostrar los controles sin cargar todos los autores y versiones solo para calcular ese permiso. Usa exactamente la política de CheckEditAccess: no concede permisos adicionales al staff.

## 2. Get proyectado

Reemplazo propuesto para el Get público:

```csharp
public async Task<OneOf<ResourceReadDTO, ResourceServiceResult>> Get(
    long resourceId,
    CancellationToken ct = default)
{
    var accessResult = await GetAccess(resourceId, ct);
    var access = CheckViewAccess(accessResult);
    if (access != ResourceServiceResult.Success)
        return access;

    bool canEdit = CheckEditAccess(accessResult)
        == ResourceServiceResult.Success;

    var result = await _dbHelperService.TryFunction(
        cancel => _context.Resources
            .AsNoTracking()
            .Where(resource => resource.Id == resourceId)
            .Select(resource => new ResourceReadDTO
            {
                Id = resource.Id,
                Name = resource.Name,
                Description = resource.Description,
                CreatedAt = resource.CreatedAt,
                UpdatedAt = resource.UpdatedAt,
                RepositoryURL = resource.RepositoryURL,
                AlreadyModerated = resource.AlreadyModerated,
                CanEdit = canEdit,

                IncludesPorn = resource.IncludesPorn,
                IncludesGore = resource.IncludesGore,
                IncludesPolitics = resource.IncludesPolitics,
                IncludesSlurs = resource.IncludesSlurs,
                IncludesSensitiveContent = resource.IncludesSensitiveContent,

                Videogame = new IdNamePair
                {
                    Id = resource.VideogameId,
                    Name = resource.Videogame!.Name ?? "Unknown"
                },
                Visibility = new IdNamePair
                {
                    Id = resource.VisibilityId,
                    Name = resource.Visibility!.Name ?? "Unknown"
                },
                Types = resource.ResourceResourceTypes
                    .OrderBy(type => type.ResourceType!.Name)
                    .ThenBy(type => type.Id)
                    .Select(type => new ResourceTypeReadDTO(
                        type.Id,
                        type.ResourceTypeId,
                        type.ResourceType!.Name ?? "Unknown",
                        type.ResourceType.CategoryId,
                        type.ResourceType.Category!.Name))
                    .ToList(),
                Tags = resource.ResourceTags
                    .OrderBy(tag => tag.Tag!.Name)
                    .ThenBy(tag => tag.Id)
                    .Select(tag => new ResourceTagReadDTO(
                        tag.Id,
                        tag.TagId,
                        tag.Tag!.Name))
                    .ToList()
            })
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancel),
        ResourceServiceResult.DBError,
        ct: ct);

    return ItemOrError(result);
}
```

Las referencias a navegaciones dentro de Select se traducen a SQL; no necesitan Include para proyectar sus campos. Las listas internas se materializan como parte de la consulta de EF, no con consultas C# independientes por cada tipo/tag. [Proyectar solo las propiedades necesarias](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying#project-only-properties-you-need).

Conservo AsSplitQuery porque hay dos colecciones: Types y Tags. Quitarlo puede multiplicar filas tipos × tags y repetir Description. Habitualmente esta forma implica una consulta del recurso y una por colección, además del GetAccess. No prometo que sea una única sentencia SQL ni una mejora de velocidad medida. [Consultas únicas y divididas](https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries).

No se hace FillUserSummaries: este DTO no contiene entidades ApplicationUser ni versiones a las que llenar resúmenes.

## 3. Consulta privada para Edit

Edit cambia relaciones y usa SaveChangesAsync. Para ese flujo necesitas seguimiento de entidades; el DTO de lectura no lo sustituye.

Propuesta de consulta privada, invocada después de la comprobación de permisos que Edit ya hace:

```csharp
private async Task<OneOf<Resource, ResourceServiceResult>> GetForEdit(
    long resourceId,
    CancellationToken ct)
{
    var result = await _dbHelperService.TryFunction(
        cancel => _context.Resources
            .AsTracking()
            .Where(resource => resource.Id == resourceId)
            .Include(resource => resource.ResourceVersions)
                .ThenInclude(version => version.File)
            .Include(resource => resource.ResourcePreviews)
            .Include(resource => resource.ResourcesAuthors)
            .Include(resource => resource.ResourceExternalAuthors)
            .Include(resource => resource.ResourceDependencies)
            .Include(resource => resource.ResourceExternalDependencies)
            .Include(resource => resource.ResourceResourceTypes)
            .Include(resource => resource.ResourceTags)
            .AsSplitQuery()
            .FirstOrDefaultAsync(cancel),
        ResourceServiceResult.DBError,
        ct: ct);

    return ItemOrError(result);
}
```

En Edit, sustituye únicamente la llamada de lectura:

```csharp
// Antes
var res = await Get(resourceId, true, ct);

// Después
var res = await GetForEdit(resourceId, ct);
```

Conserva CheckEditAccess antes de esa llamada. GetForEdit es privado y no es una acción HTTP.

Aquí los Includes sirven para guardar cambios en las relaciones existentes. No se cargan Submitter, Videogame, Visibility, categorías, nombres de tipos ni entidades Tag porque la edición actual usa sus IDs y genera las incorporaciones por sus métodos propios. ResourceVersions.File sí se conserva: GetMainResourceFolder lo utiliza.

## 4. Firma del controlador

La URL permanece igual. Solo cambia el tipo de la respuesta:

```csharp
[HttpGet("{resourceId:long}")]
public async Task<ActionResult<ResourceReadDTO>> Get(
    [FromRoute] long resourceId,
    CancellationToken ct = default)
{
    var result = await _resourceService.Get(resourceId, ct);
    return GetActionResult(result);
}
```

El mapper genérico existente ya admite ese OneOf.

## 5. Adaptación posterior del frontend

Cambiar el tipo no es compatible automáticamente con el frontend actual: hoy deserializa Resource. Cuando apliques el contrato, corresponde adaptar:

- ResourceDetails: ResourceReadDTO, Types/Tags planos, categorías desde Types y CanEdit para los botones de gestión. Rating y las demás secciones siguen usando sus acciones.
- ResourceEditor y ResourceEditorSelectionSnapshot: usar Videogame.Id y Visibility.Id, y conservar los IDs de relación de Types/Tags al enviar eliminaciones.
- PageAccessGuard y AccountPageContext: deserializar el DTO y comprobar CanEdit para mostrar las páginas de gestión. El editor seguirá obteniendo autores/versiones para sus formularios, pero no será necesario traer esas listas solamente para resolver el permiso del guard.
- ResourceManagementAccess: usar el indicador CanEdit de la respuesta actual, junto a una sesión verificada, en vez de recalcularlo desde colecciones.
- El bloque Used by de la vista actual consulta resource.Dependents, pero Get no carga esa colección. No añadí un campo de dependientes ficticio al DTO; si quieres activar esa sección, requiere su contrato específico.

CanEdit controla la presentación; Edit, AddVersion y EditVersion deben seguir validando permisos en backend en cada petición. Una comprobación de permisos seguida de una consulta no es una transacción ni garantiza una instantánea frente a cambios simultáneos de visibilidad; esta propuesta mantiene el patrón actual del servicio.

## Mejora esperada y límites

- Get no devuelve un grafo de entidades ni colecciones vacías de partes que llegan por otros endpoints.
- Videogame y Visibility contienen solo Id y Name; tipos y categorías contienen los campos mostrados.
- Elimina el problema de construir resúmenes de versiones no cargadas en Get.
- CanEdit puede reducir las consultas dedicadas a resolver permisos en el frontend.
- El resultado es menor y más fácil de mantener. La mejora exacta debe medirse con tus datos; no se compiló ni ejecutó este ejemplo como parte de la propuesta.

Una mejora adicional sería resolver Visibility y los nombres/categorías de tipos usando CacheService, consultando en SQL solamente los IDs de sus asociaciones. No está incluida en este primer cambio para que puedas aplicar y revisar la proyección de forma independiente.
