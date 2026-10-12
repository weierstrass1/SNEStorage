# SNEStorage: propuesta de fixes de rendimiento

Fecha: **2026-10-08**. Código revisado en `D:/Users/pndia/Documents/GitHub/SNEStorage`.

Este documento desarrolla los **29 casos** de las tablas de la revisión anterior, en el mismo orden de prioridad. Contiene código propuesto para que lo revises, lo adaptes y decidas qué aplicar. **No se aplicaron estos cambios al backend.** Los ejemplos no se compilaron ni se ejecutaron contra tu base de datos.

Las líneas corresponden a la copia leída hoy; algunos números cambiaron respecto de la lista anterior. En cada punto indico también el texto que debes buscar. Los ejemplos de reemplazo de un bloque se integran en el método existente: no reemplazan sus validaciones, rollback, autorización o respuesta de errores salvo que lo indique expresamente.

## Cómo leer y aplicar la propuesta

- **P1, alta:** reducir lecturas y respuestas que crecen con todos los votos, comentarios o archivos de un recurso.
- **P2, media:** separar consultas de lectura/escritura y reducir consultas repetidas por elemento.
- **P3, baja:** reducir consultas pequeñas o menos frecuentes; después de los casos anteriores.
- Son prioridades por tamaño potencial y frecuencia de uso, no tiempos medidos de cada método. El log anterior no demuestra que cada caso sea actualmente lento.
- No propongo caché en backend. `Select`, `Count`, `Average` y `Any` consultan la base de datos; no almacenan resultados para reutilizarlos entre solicitudes.
- No ejecutes consultas concurrentes con el mismo `SnestorageContext`. Los ejemplos realizan sus consultas secuencialmente.
- Mantengo `VisibilityId == 3` como privado. Los no listados pueden consultarse mediante enlaces y búsquedas de dependencias según tu política actual.
- Mantengo permisos de edición para autores, submitter del recurso y submitters de versiones. Que un moderador pueda **ver** no lo convierte automáticamente en editor.
- Conservo el esquema de errores con enums y `OneOf`. Un resultado inexistente sigue siendo distinto de un fallo de consulta.
- Los métodos que escriben necesitan entidades con seguimiento; los que devuelven DTOs de lectura no deben reutilizarse para editar entidades.

### Dependencias entre puntos

| Grupo | Aplicación recomendada |
|---|---|
| 1 y 6 | Estadísticas de estrellas + contrato del detalle + adaptación del frontend. |
| 2, 8 y 22 | Un mismo DTO de comentario para listar, publicar y responder; adaptación del frontend. |
| 3 | Puede optimizarse conservando la respuesta actual de un voto. |
| 4 | Consulta de un archivo + su consumidor `Download` y firma del controlador. |
| 5 y 14 | Respuestas mínimas de creación; son los cambios más pequeños. |
| 7 y 9 | Permisos de comentarios calculados en SQL, sin cargar colecciones. |
| 11, 12 y 13 | Separar consultas de edición del GET público y consultar la carpeta base por separado. |
| 17–20 | Una consulta por conjunto de IDs/nombres, no una por elemento. |
| 24–26 | Perfil privado, sesión mínima y resumen público son tres necesidades distintas. |

**Importante para las vistas:** hoy varias partes del frontend deserializan `Resource`, `Comment` o `UserInfo`, y leen listas de votos. No debes cambiar una respuesta a estos nuevos DTOs y esperar que esas vistas funcionen sin adaptación. Al final hay una lista concreta de consumidores. Los ejemplos de DTO son contratos propuestos, no DTOs ya existentes.

### Tipos compartidos propuestos

Estos tipos se añadirían al namespace `SNEStorage.DTOs`, en archivos separados si los aplicas. No son entidades ni requieren migración. Los ejemplos usan los `using` habituales de tu proyecto: EF Core, `OneOf`, `SNEStorage.Models` y `SNEStorage.DTOs`.

```csharp
namespace SNEStorage.DTOs;

public sealed record ResourceCreatedDto(long Id);
public sealed record VersionCreatedDto(long Id, long ResourceId);
public sealed record ResourceChoiceDto(long Id, string Name);
public sealed record RatingReadDto(double Average, int VoteCount, long? MyRating);

// URL es una ruta relativa al ContentRoot, como en tu File actual.
// No significa que los archivos de Storage sean públicos o estáticos.
public sealed record ResourceFileReadDto(
    long Id, string URL, long FileTypeId, string MimeType);

public sealed class CommentReadDto
{
    public long Id { get; init; }
    public required string UserId { get; init; }
    public long ScoreId { get; init; }
    public required string Text { get; init; }
    public bool Deleted { get; init; }
    public DateTime PublishDate { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public int Likes { get; init; }
    public int Dislikes { get; init; }
    public bool? MyVote { get; init; } // true / false / null = no voto
    public bool HasReplies { get; init; }
    public UserNameDTO? UserSummary { get; init; }
}

public sealed record ReplyCreatedDto(long ParentCommentId, CommentReadDto Comment);
```

Dentro de `ResourceService`, propongo un helper para los puntos 3, 4, 6 y 11–13. Devuelve escalares; no descarga autores ni versiones. `Any` se traduce a SQL. Las llamadas a `_accountService` se evalúan antes de construir la expresión SQL.

```csharp
private sealed record ResourceAccessRow(
    long Id, long ScoreId, long VisibilityId, bool IsContributor);

private Task<OneOf<ResourceAccessRow?, ResourceServiceResult>>
    GetAccessAsync(long resourceId, CancellationToken ct)
{
    var userId = _accountService.GetUserID();
    return _dbHelperService.TryFunction(
        cancel => _context.Resources.AsNoTracking()
            .Where(r => r.Id == resourceId)
            .Select(r => new ResourceAccessRow(
                r.Id, r.ScoreId, r.VisibilityId,
                userId != null && (
                    r.SubmitterId == userId ||
                    r.ResourcesAuthors.Any(a => a.UserId == userId) ||
                    r.ResourceVersions.Any(v => v.SubmitterUserId == userId))))
            .FirstOrDefaultAsync(cancel),
        ResourceServiceResult.DBError, ct: ct);
}

private ResourceServiceResult CheckViewAccess(ResourceAccessRow? access)
{
    if (access is null)
        return ResourceServiceResult.ResourceNotFound;
    if (access.VisibilityId != 3 || _accountService.IsModeratorOrAdmin())
        return ResourceServiceResult.Success;
    if (_accountService.GetUserID() is null)
        return ResourceServiceResult.UserNotFound;
    return access.IsContributor
        ? ResourceServiceResult.Success : ResourceServiceResult.Unauthorized;
}

private ResourceServiceResult CheckEditAccess(ResourceAccessRow? access)
{
    if (access is null)
        return ResourceServiceResult.ResourceNotFound;
    if (_accountService.GetUserID() is null)
        return ResourceServiceResult.UserNotFound;
    return access.IsContributor
        ? ResourceServiceResult.Success : ResourceServiceResult.Unauthorized;
}
```

No añadas este helper a `CommentService` llamando directamente a `ResourceService` solo para compartirlo. En el punto 7 doy la consulta equivalente en ese servicio. Una extracción común de permisos sería una refactorización posterior si tú la quieres.

---

## 1. Estadísticas de estrellas sin cargar todos los votos — P1

**Clase:** `ResourceService`.
**Método:** `Get`.
**Línea actual:** [ResourceService.cs:449](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:449).

```csharp
.Include(r => r.Score)
    .ThenInclude(r => r!.ScoresUsers)
```

**Datos necesarios:** promedio, número de votos y voto del usuario actual. El detalle no necesita la lista de IDs y usuarios de todos los votantes.

### Fix propuesto

Quitar la carga de `ScoresUsers` del **GET de lectura** y devolver `RatingReadDto`. Un helper concreto, utilizable después de comprobar permisos:

```csharp
private Task<RatingReadDto?> GetRatingAsync(long resourceId, CancellationToken ct)
{
    var userId = _accountService.GetUserID();
    return _context.Resources.AsNoTracking()
        .Where(r => r.Id == resourceId)
        .Select(r => new RatingReadDto(
            r.Score!.ScoresUsers.Where(v => v.Value >= 1 && v.Value <= 5)
                .Average(v => (double?)v.Value) ?? 0.0,
            r.Score.ScoresUsers.Count(v => v.Value >= 1 && v.Value <= 5),
            r.Score.ScoresUsers
                .Where(v => userId != null && v.UserId == userId && v.Value >= 1 && v.Value <= 5)
                .Select(v => (long?)v.Value)
                .FirstOrDefault()))
        .FirstOrDefaultAsync(ct);
}
```

En el punto 6 integro estos mismos cálculos en la proyección del detalle, por lo que **no necesitas ejecutar también este helper** si aplicas esa versión. No hay `ToList` de votos: SQL calcula los agregados y devuelve tres valores. Mantengo el filtro de valores válidos de 1 a 5 que usa hoy `ResourceRating`.

**Contrato/frontend:** sustituir el cálculo desde `score.scoresUsers` por `rating.average`, `rating.voteCount` y `rating.myRating`. Mantener `0` y `Not rated` cuando `voteCount == 0`.
**Dependencia:** separar primero el GET usado para edición, punto 11. No retirar relaciones indiscriminadamente de un método compartido que todavía las necesita para escribir.
**Resultado esperado:** tamaño de respuesta constante respecto de la cantidad de votantes. El coste SQL del agregado sigue dependiendo de filas e índices.

## 2. Comentarios paginados con agregados y HasReplies — P1

**Clase:** `CommentService`.
**Métodos:** `GetStart`, `GetEnd`, usados por `GetResourceComments` y `GetReplies`.
**Líneas actuales:** [CommentService.cs:256](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:256), [257](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:257) y [269](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:269).

```csharp
.Include(r => r.Score).ThenInclude(r => r!.LikesUsers)
.Include(r => r.Childrens)
// ...
.Skip(maxAmount * page).Take(maxAmount).ToListAsync(cancel)
```

**Datos necesarios:** texto, fechas, autor con color/avatar, likes, dislikes, voto propio y existencia de respuestas. No hacen falta todos los votos ni todas las entidades de respuestas.

### Fix propuesto

En `GetStart`, mantener las validaciones actuales y reemplazar exclusivamente la construcción de consulta:

```csharp
IQueryable<Comment> query = _context.Comments.AsNoTracking();
return OneOf<IQueryable<Comment>, CommentServiceResult>.FromT0(query);
```

Añadir este helper de proyección dentro de `CommentService`. No llamar `UserNameDTO.FromApplicationUser` dentro del árbol SQL; seleccionamos sus campos directamente.

```csharp
private IQueryable<CommentReadDto> ProjectComments(IQueryable<Comment> query)
{
    var userId = _accountService.GetUserID();
    return query.Select(c => new CommentReadDto
    {
        Id = c.Id, UserId = c.UserId, ScoreId = c.ScoreId,
        Text = c.Deleted ? "" : c.Text, Deleted = c.Deleted,
        PublishDate = c.PublishDate, UpdatedAt = c.UpdatedAt,
        Likes = c.Score!.LikesUsers.Count(v => v.Value),
        Dislikes = c.Score.LikesUsers.Count(v => !v.Value),
        MyVote = c.Score.LikesUsers
            .Where(v => userId != null && v.UserId == userId)
            .Select(v => (bool?)v.Value).FirstOrDefault(),
        HasReplies = c.Childrens.Any(),
        UserSummary = c.User == null ? null : new UserNameDTO
        {
            Id = c.UserId, Username = c.User.UserName!,
            UsernameColor = c.User.UserInfo == null
                ? null : c.User.UserInfo.UsernameColor,
            Avatar = c.User.UserInfo == null || c.User.UserInfo.Avatar == null
                ? null : new FileResponse
                {
                    Id = c.User.UserInfo.Avatar.Id,
                    URL = c.User.UserInfo.Avatar.URL,
                    FileTypeId = c.User.UserInfo.Avatar.FileTypeId
                }
        }
    });
}

private async Task<OneOf<List<CommentReadDto>, CommentServiceResult>> GetEnd(
    IQueryable<Comment> query, int maxAmount, int page, CommentSorting sort,
    CancellationToken ct = default)
{
    // Un borrado sin hijos no se muestra; un borrado con hijos conserva el hilo.
    query = query.Where(c => !c.Deleted || c.Childrens.Any());
    return await _dbHelperService.TryFunction(
        cancel => ProjectComments(SortComments(query, sort)
            .Skip((int)((long)maxAmount * page)).Take(maxAmount))
            .ToListAsync(cancel),
        CommentServiceResult.DBError, ct: ct);
}
```

Mantener `SortComments` como `IOrderedQueryable<Comment>`: ordenar y paginar antes de materializar. Cambiar las firmas de `GetResourceComments`, `GetReplies` y sus acciones a `List<CommentReadDto>`; sus filtros y autorización permanecen iguales.

**Contrato/frontend:** leer los agregados, `myVote` y `hasReplies`, conservar avatar y color. Ya no derivar esos valores de `score.likesUsers` y `childrens`. Esto también puede evitar la petición por comentario a `GetUserLike` al renderizar una página: el voto viene en la página. El endpoint puede conservarse para consultas aisladas.
**Resultado esperado:** la página devuelve hasta `maxAmount` comentarios, no los votos y todas las respuestas asociados a cada uno. El porcentaje se calcula en frontend con `likes / (likes + dislikes)`; no mostrarlo si ambos son cero.

## 3. Registrar estrellas leyendo solo permisos y el voto propio — P1

**Clase:** `ResourceService`.
**Método:** `AddOrEditScore`.
**Línea actual:** [ResourceService.cs:143](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:143).

```csharp
.Include(r => r.ResourcesAuthors)
.Include(r => r.ResourceVersions)
.Include(r => r.Score).ThenInclude(s => s!.ScoresUsers)
```

**Datos necesarios:** `ScoreId`, visibilidad, si el usuario es autor/submitter y una única fila `ScoreUsers` de ese usuario.

### Fix propuesto: reemplazo completo compatible con la respuesta actual

```csharp
public async Task<OneOf<ScoreUsers, ResourceServiceResult>> AddOrEditScore(
    long resourceId, long value, CancellationToken ct = default)
{
    if (value is < 1 or > 5)
        return ResourceServiceResult.InvalidScore;
    var userId = _accountService.GetUserID();
    if (userId is null)
        return ResourceServiceResult.Unauthorized;

    var accessResult = await GetAccessAsync(resourceId, ct);
    if (!accessResult.TryPickT0(out var access, out var error))
        return error;
    if (access is null)
        return ResourceServiceResult.ResourceNotFound;
    if (access.VisibilityId == 3)
        return ResourceServiceResult.CantAddScoreToPrivateResource;
    if (access.IsContributor)
        return ResourceServiceResult.CantScoreSelfResource;

    var voteResult = await _dbHelperService.TryFunction(
        cancel => _context.ScoresUsers.FirstOrDefaultAsync(
            v => v.ScoreId == access.ScoreId && v.UserId == userId, cancel),
        ResourceServiceResult.DBError, ct: ct);
    if (!voteResult.TryPickT0(out var vote, out error))
        return error;
    if (vote is null)
    {
        vote = new ScoreUsers
        {
            Id = 0, ScoreId = access.ScoreId, UserId = userId
        };
        _context.ScoresUsers.Add(vote);
    }
    vote.Value = value;
    return await _dbHelperService.TrySyncContext(
        vote, ResourceServiceResult.DBError, ct);
}
```

**Contrato/frontend:** este reemplazo mantiene la respuesta de voto, pero ya no lleva un grafo de `Score`. Para actualizar promedio/contador sin recargar todo el recurso, se puede exponer el helper del punto 1 como un GET pequeño de estadísticas, autorizado previamente, o acordar una respuesta de mutación con estadísticas.

Si eliges consultar estadísticas después de guardar dentro del POST, un fallo en esa segunda consulta **no revierte el voto ya guardado**. No devolverlo como si el guardado hubiera fallado: distinguir éxito de escritura de fallo al refrescar estadísticas. Mi primera aplicación sería mantener la respuesta mínima del voto y refrescar los agregados mediante lectura separada.
**Resultado esperado:** votar no carga todos los votos del recurso. La concurrencia de dos votos del mismo usuario se resuelve con la restricción única y política de escritura que definas, no con la proyección.

## 4. Buscar un solo archivo del recurso — P1

**Clase:** `ResourceService`; consumidor `ResourceController`.
**Métodos:** `GetFile`, `Download`.
**Líneas actuales:** [ResourceService.cs:487](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:487) y [496](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:496).

```csharp
.Include(r => r.ResourceVersions).ThenInclude(version => version.File)
.Include(r => r.ResourcePreviews).ThenInclude(preview => preview.File)
.Include(r => r.ResourcesAuthors)
```

**Datos necesarios:** permisos, pertenencia del archivo solicitado al recurso, `Id`, `URL`, `FileTypeId` y MIME de ese archivo. No hace falta cargar todas las versiones/previews.

### Fix propuesto: reemplazo completo

```csharp
public async Task<OneOf<ResourceFileReadDto, ResourceServiceResult>> GetFile(
    long resourceId, long resourceFileId, CancellationToken ct = default)
{
    var accessResult = await GetAccessAsync(resourceId, ct);
    if (!accessResult.TryPickT0(out var access, out var error))
        return error;
    var permission = CheckViewAccess(access);
    if (permission != ResourceServiceResult.Success)
        return permission;

    var result = await _dbHelperService.TryFunction(
        cancel => _context.Files.AsNoTracking()
            .Where(f => f.Id == resourceFileId && (
                _context.ResourceVersions.Any(v =>
                    v.ResourceId == resourceId && v.FileId == f.Id) ||
                _context.ResourcePreviews.Any(p =>
                    p.ResourceId == resourceId && p.FileId == f.Id)))
            .Select(f => new ResourceFileReadDto(
                f.Id, f.URL, f.FileTypeId,
                f.FileType == null ? "application/octet-stream" : f.FileType.Value))
            .FirstOrDefaultAsync(cancel),
        ResourceServiceResult.DBError, ct: ct);
    if (!result.TryPickT0(out var file, out error))
        return error;
    return file is null ? ResourceServiceResult.FileNotFound : file;
}

public async Task<OneOf<(string, string), ResourceServiceResult>> Download(
    long resourceId, long resourceFileId, CancellationToken ct = default)
{
    var result = await GetFile(resourceId, resourceFileId, ct);
    if (!result.TryPickT0(out var file, out var error))
        return error;
    var physicalPath = Path.Combine(_environment.ContentRootPath, file.URL);
    if (!System.IO.File.Exists(physicalPath))
        return ResourceServiceResult.FileNotFound;
    return (physicalPath, file.MimeType);
}
```

Eliminar el parámetro `prepareDownload`, que ya no cambia la consulta. Adaptar todas sus llamadas. En `ResourceController.GetFile`, corregir la firma a `ActionResult<OneOf<ResourceFileReadDto, ResourceServiceResult>>` y la rama de error a `GetActionResult<ResourceFileReadDto>(error)`. Hoy esa firma dice `Resource` aunque devuelve un archivo.

**Contrato/frontend:** mantener las rutas `api/resource/{resourceId}/file/{resourceFileId}` y `/download`. El primer endpoint sigue devolviendo JSON; el de descarga mantiene `PhysicalFile(..., enableRangeProcessing: true)`. Adaptar el consumidor del MIME a `mimeType`; conservar `id`, `url`, `fileTypeId`. No convertir una URL relativa a `Storage` en acceso público directo.
**Resultado esperado:** por preview/descarga se consulta un archivo, no toda la colección. El tamaño del video y la lectura física son costes distintos de esta consulta.

## 5. Respuesta mínima después de crear recurso — P1

**Clase:** `ResourceController`.
**Método:** `CreateResource`.
**Línea actual:** [ResourceController.cs:148](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Controllers/ResourceController.cs:148).

```csharp
return Ok(response);
```

**Datos necesarios:** ID del recurso creado para navegar a `/Resource/Created/{id}`. No el recurso con todas las relaciones recién creadas.

### Fix propuesto

Conservar los atributos actuales de ruta, autenticación y tamaños. Reemplazar firma/cuerpo por:

```csharp
public async Task<ActionResult<OneOf<ResourceCreatedDto, ResourceServiceResult>>>
    CreateResource([FromForm] ResourceCreateDTO dto, CancellationToken ct)
{
    var result = await _resourceService.Create(dto, ct);
    if (result.TryPickT0(out var resource, out var error))
        return Ok(new ResourceCreatedDto(resource.Id));
    return GetActionResult<ResourceCreatedDto>(error);
}
```

El servicio puede seguir construyendo y guardando `Resource` con relaciones; se recorta **la respuesta HTTP** después del guardado. No cambies el orden del `SaveChanges`, batch o rollback por esta optimización.
**Contrato/frontend:** JSON `{ "id": 123 }`. El flujo actual de creación ya extrae el ID; revisar su adaptador antes de aplicar. No afecta tiempo de transferencia del archivo hacia el servidor; sí evita serializar un grafo completo al responder.

## 6. DTO del detalle con datos visuales y sin entidades Identity — P2

**Clase:** `ResourceService`.
**Método:** `Get` como lectura para la vista; propuesto `GetDetails`.
**Líneas actuales:** [ResourceService.cs:445](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:445), [453](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:453), [459](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:459), [461](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:461).

```csharp
.Include(r => r.ResourceVersions).ThenInclude(v => v.SubmitterUser)
.Include(r => r.ResourcesAuthors).ThenInclude(a => a.User)
.Include(r => r.ResourceDependencies).ThenInclude(d => d.DependencyResource)
// Al final: FillUserSummaries y FromApplicationUser(resource.Submitter!)
```

**Datos necesarios:** campos visibles/editables, categorías/tipos, tags, nombres/colores de autores y submitters, versiones, archivos mínimos, previews, dependencias y flags. No `ApplicationUser`, sus campos de autenticación ni navegaciones inversas. **Versiones y archivos sí se necesitan**, pero solo sus campos usados.

### Fix propuesto

Para no quitar funciones al editor, conservar IDs de las filas de asociación (son los IDs usados en `Remove...IDs`). Contrato de lectura propuesto:

```csharp
public sealed record TypeReadDto(
    long Id, long ResourceTypeId, string? Name, long CategoryId, string? CategoryName);
public sealed record TagReadDto(long Id, long TagId, string? Name);
public sealed record AuthorReadDto(long Id, string UserId, UserNameDTO UserSummary);
public sealed record PreviewReadDto(long Id, long FileId, ResourceFileReadDto File);
public sealed record DependencyReadDto(long Id, long DependencyResourceId, string? Name);
public sealed record ExternalAuthorReadDto(long Id, string Author);
public sealed record ExternalDependencyReadDto(
    long Id, string DependencyName, string DependencyURL,
    string? Mirror1, string? Mirror2, string? Mirror3, string? Mirror4);
public sealed record VersionReadDto(
    long Id, long ResourceId, long FileId, string SubmitterUserId,
    string? Version, string? ChangeLog, int Downloads,
    DateTime CreatedAt, DateTime? UpdatedAt,
    bool AlreadyModerated, bool SealOfQualityRequested, bool SealOfQuality,
    UserNameDTO UserSummary, ResourceFileReadDto File);

public sealed class ResourceDetailsReadDto
{
    public long Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public long VideogameId { get; init; }
    public string? VideogameName { get; init; }
    public long VisibilityId { get; init; }
    public string? VisibilityName { get; init; }
    public required string SubmitterId { get; init; }
    public UserNameDTO? UserSummary { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public bool AlreadyModerated { get; init; }
    public bool IncludesPorn { get; init; }
    public bool IncludesGore { get; init; }
    public bool IncludesPolitics { get; init; }
    public bool IncludesSlurs { get; init; }
    public bool IncludesSensitiveContent { get; init; }
    public string? RepositoryURL { get; init; }
    public bool CanEdit { get; init; }
    public RatingReadDto? Rating { get; init; }
    public List<TypeReadDto> Types { get; init; } = [];
    public List<TagReadDto> Tags { get; init; } = [];
    public List<AuthorReadDto> Authors { get; init; } = [];
    public List<ExternalAuthorReadDto> ExternalAuthors { get; init; } = [];
    public List<DependencyReadDto> Dependencies { get; init; } = [];
    public List<ExternalDependencyReadDto> ExternalDependencies { get; init; } = [];
    public List<PreviewReadDto> Previews { get; init; } = [];
    public List<VersionReadDto> Versions { get; init; } = [];
}
```

Método nuevo de lectura. **No sustituye la carga con tracking del punto 11**. El usuario actual y el rol se capturan fuera de SQL. No expone avatares de autores/versiones en este GET.

```csharp
public async Task<OneOf<ResourceDetailsReadDto, ResourceServiceResult>> GetDetails(
    long resourceId, CancellationToken ct = default)
{
    var accessResult = await GetAccessAsync(resourceId, ct);
    if (!accessResult.TryPickT0(out var access, out var error))
        return error;
    var permission = CheckViewAccess(access);
    if (permission != ResourceServiceResult.Success)
        return permission;

    var userId = _accountService.GetUserID();
    var isStaff = _accountService.IsModeratorOrAdmin();
    var result = await _dbHelperService.TryFunction(
        cancel => _context.Resources.AsNoTracking().AsSplitQuery()
            // Repetir el filtro impide devolver datos si cambiaron permisos
            // entre la consulta de acceso y esta consulta.
            .Where(r => r.Id == resourceId && (r.VisibilityId != 3 || isStaff ||
                (userId != null && (r.SubmitterId == userId ||
                    r.ResourcesAuthors.Any(a => a.UserId == userId) ||
                    r.ResourceVersions.Any(v => v.SubmitterUserId == userId)))))
            .Select(r => new ResourceDetailsReadDto
            {
                Id = r.Id, Name = r.Name, Description = r.Description,
                VideogameId = r.VideogameId, VideogameName = r.Videogame!.Name,
                VisibilityId = r.VisibilityId, VisibilityName = r.Visibility!.Name,
                SubmitterId = r.SubmitterId,
                UserSummary = r.Submitter == null ? null : new UserNameDTO
                {
                    Id = r.SubmitterId, Username = r.Submitter.UserName!,
                    UsernameColor = r.Submitter.UserInfo == null
                        ? null : r.Submitter.UserInfo.UsernameColor
                },
                CreatedAt = r.CreatedAt, UpdatedAt = r.UpdatedAt,
                AlreadyModerated = r.AlreadyModerated,
                IncludesPorn = r.IncludesPorn, IncludesGore = r.IncludesGore,
                IncludesPolitics = r.IncludesPolitics, IncludesSlurs = r.IncludesSlurs,
                IncludesSensitiveContent = r.IncludesSensitiveContent,
                RepositoryURL = r.RepositoryURL,
                CanEdit = userId != null && (r.SubmitterId == userId ||
                    r.ResourcesAuthors.Any(a => a.UserId == userId) ||
                    r.ResourceVersions.Any(v => v.SubmitterUserId == userId)),
                Rating = new RatingReadDto(
                    r.Score!.ScoresUsers.Where(v => v.Value >= 1 && v.Value <= 5)
                        .Average(v => (double?)v.Value) ?? 0,
                    r.Score.ScoresUsers.Count(v => v.Value >= 1 && v.Value <= 5),
                    r.Score.ScoresUsers.Where(v => userId != null && v.UserId == userId &&
                        v.Value >= 1 && v.Value <= 5)
                        .Select(v => (long?)v.Value).FirstOrDefault()),
                Types = r.ResourceResourceTypes.OrderBy(t => t.Id)
                    .Select(t => new TypeReadDto(t.Id, t.ResourceTypeId,
                        t.ResourceType!.Name, t.ResourceType.CategoryId,
                        t.ResourceType.Category!.Name)).ToList(),
                Tags = r.ResourceTags.OrderBy(t => t.Id)
                    .Select(t => new TagReadDto(t.Id, t.TagId, t.Tag!.Name)).ToList(),
                Authors = r.ResourcesAuthors.OrderBy(a => a.Id)
                    .Select(a => new AuthorReadDto(a.Id, a.UserId, new UserNameDTO
                    {
                        Id = a.UserId, Username = a.User!.UserName!,
                        UsernameColor = a.User.UserInfo == null
                            ? null : a.User.UserInfo.UsernameColor
                    })).ToList(),
                ExternalAuthors = r.ResourceExternalAuthors.OrderBy(a => a.Id)
                    .Select(a => new ExternalAuthorReadDto(a.Id, a.Author)).ToList(),
                Dependencies = r.ResourceDependencies.OrderBy(d => d.Id)
                    .Select(d => new DependencyReadDto(d.Id, d.DependencyResourceId,
                        d.DependencyResource!.VisibilityId != 3 || isStaff ||
                        (userId != null && (
                            d.DependencyResource.SubmitterId == userId ||
                            d.DependencyResource.ResourcesAuthors.Any(a => a.UserId == userId) ||
                            d.DependencyResource.ResourceVersions.Any(v => v.SubmitterUserId == userId)))
                            ? d.DependencyResource.Name : null)).ToList(),
                ExternalDependencies = r.ResourceExternalDependencies.OrderBy(d => d.Id)
                    .Select(d => new ExternalDependencyReadDto(d.Id, d.DependencyName,
                        d.DependencyURL, d.Mirror1, d.Mirror2, d.Mirror3, d.Mirror4)).ToList(),
                Previews = r.ResourcePreviews.OrderBy(p => p.Id)
                    .Select(p => new PreviewReadDto(p.Id, p.FileId,
                        new ResourceFileReadDto(p.File!.Id, p.File.URL, p.File.FileTypeId,
                            p.File.FileType == null ? "application/octet-stream" : p.File.FileType.Value)))
                    .ToList(),
                Versions = r.ResourceVersions.OrderByDescending(v => v.CreatedAt)
                    .ThenByDescending(v => v.Id).Select(v => new VersionReadDto(
                        v.Id, v.ResourceId, v.FileId, v.SubmitterUserId,
                        v.Version, v.ChangeLog, v.Downloads, v.CreatedAt, v.UpdatedAt,
                        v.AlreadyModerated, v.SealOfQualityRequested, v.SealOfQuality,
                        new UserNameDTO
                        {
                            Id = v.SubmitterUserId, Username = v.SubmitterUser!.UserName!,
                            UsernameColor = v.SubmitterUser.UserInfo == null
                                ? null : v.SubmitterUser.UserInfo.UsernameColor
                        },
                        new ResourceFileReadDto(v.File!.Id, v.File.URL, v.File.FileTypeId,
                            v.File.FileType == null ? "application/octet-stream" : v.File.FileType.Value)))
                    .ToList()
            }).FirstOrDefaultAsync(cancel),
        ResourceServiceResult.DBError, ct: ct);
    if (!result.TryPickT0(out var details, out error))
        return error;
    // No revelamos detalle si dejó de ser visible durante la operación.
    return details is null ? ResourceServiceResult.ResourceNotFound : details;
}
```

**Contrato/frontend:** `ResourceController.Get` llamaría `GetDetails` y usaría el nuevo tipo en su firma/helper. Adaptar detalle, editor y guard de permisos a este contrato; `CanEdit` evita reconstruir reglas en distintas vistas. La autorización al guardar sigue siendo del servidor. Las dependencias cuyo nombre no es visible se muestran como `Private resource`, sin revelar nombre/descripción ni hacer fetch del detalle.

**Detalle de la revisión actual:** tu `Get` llama `FromApplicationUser(resource.Submitter!)` pero ahora no incluye `Submitter`. Si no hay carga previa/fixup, puede ser null. La proyección anterior obtiene el resumen directamente. Asimismo, seleccionar `UsernameColor` evita depender de un `UserInfo` no cargado. Es una corrección funcional asociada a esta consulta.

**Resultado esperado:** menos columnas/materialización y sin grafos Identity o inversos. `AsSplitQuery` sigue separando colecciones; no elimina viajes SQL ni garantiza una única fotografía consistente entre todos ellos. Si el historial crece mucho, paginar versiones sería un contrato adicional, no parte de este ejemplo.

## 7. Permisos de comentarios sin cargar autores y versiones — P2

**Clase:** `CommentService`.
**Método:** `CheckResourcePermission`.
**Línea actual:** [CommentService.cs:219](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:219).

```csharp
.Include(r => r.ResourceVersions)
.Include(r => r.ResourcesAuthors)
```

**Datos necesarios:** si existe el recurso y un booleano de acceso. Mantener diferencia entre inexistencia, denegación y DBError.

### Fix propuesto: reemplazo completo

```csharp
private async Task<CommentServiceResult> CheckResourcePermission(
    long resourceId, CancellationToken ct)
{
    var userId = _accountService.GetUserID();
    var isStaff = _accountService.IsModeratorOrAdmin();
    var result = await _dbHelperService.TryFunction(
        cancel => _context.Resources.AsNoTracking()
            .Where(r => r.Id == resourceId)
            .Select(r => new
            {
                r.Id,
                CanView = r.VisibilityId != 3 || isStaff ||
                    (userId != null && (r.SubmitterId == userId ||
                        r.ResourceVersions.Any(v => v.SubmitterUserId == userId) ||
                        r.ResourcesAuthors.Any(a => a.UserId == userId)))
            }).FirstOrDefaultAsync(cancel),
        CommentServiceResult.DBError, ct: ct);
    if (!result.TryPickT0(out var access, out var error))
        return error;
    if (access is null)
        return CommentServiceResult.ResourceNotFound;
    return access.CanView
        ? CommentServiceResult.Success : CommentServiceResult.Unauthorized;
}
```

**Contrato/frontend:** no cambia. Las reglas son las mismas que tu método actual; `Any` evalúa relaciones en SQL sin `Include`.

## 8. Publicar un comentario sin cargar entidades de usuario y recurso — P2

**Clase:** `ResourceService`.
**Método:** `AddComment`.
**Líneas actuales:** [ResourceService.cs:84](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:84) y [95](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:95).

```csharp
.Include(u => u.UserInfo).ThenInclude(u => u!.Avatar)
// Otra consulta:
.Include(r => r.ResourcesAuthors).Include(r => r.ResourceVersions)
```

**Datos necesarios:** permiso del recurso y resumen público del usuario (incluye avatar en comentarios). Para insertar bastan `ResourceId`, `UserId` y entidades nuevas `Comment`/`Score`.

### Fix propuesto: reemplazo completo

Depende del `GetUserSummary` del punto 26 y de `CommentReadDto` del punto 2.

```csharp
public async Task<OneOf<CommentReadDto, ResourceServiceResult>> AddComment(
    long resourceId, string commentText, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(commentText))
        return ResourceServiceResult.EmptyComment;
    var userId = _accountService.GetUserID();
    if (userId is null)
        return ResourceServiceResult.Unauthorized;
    var accessResult = await GetAccessAsync(resourceId, ct);
    if (!accessResult.TryPickT0(out var access, out var error))
        return error;
    var permission = CheckViewAccess(access);
    if (permission != ResourceServiceResult.Success)
        return permission;

    var summaryResult = await _accountService.GetUserSummary(userId, ct);
    if (!summaryResult.TryPickT0(out var summary, out var summaryError))
        return summaryError == AccountServiceResult.UserNotFound
            ? ResourceServiceResult.UserNotFound : ResourceServiceResult.DBError;

    var comment = new Comment
    {
        UserId = userId, Text = commentText,
        PublishDate = DateTime.UtcNow, Score = new Score()
    };
    _context.ResourcesComments.Add(new ResourceComments
    {
        Id = 0, ResourceId = resourceId, Comment = comment
    });
    if (!await _dbHelperService.TrySyncContext(ct))
        return ResourceServiceResult.DBError;
    return new CommentReadDto
    {
        Id = comment.Id, UserId = userId, ScoreId = comment.ScoreId,
        Text = comment.Text, PublishDate = comment.PublishDate,
        Deleted = false, HasReplies = false, Likes = 0, Dislikes = 0,
        MyVote = null, UserSummary = summary
    };
}
```

**Contrato/frontend:** el POST devuelve el comentario directamente, no una asociación que contiene `comment`. Actualizar la firma y `GetActionResult<CommentReadDto>` de `ResourceController.AddComment`, y el adaptador en `comment-api.js`/`CommentApiClient`. El frontend ya puede insertar el DTO en su feed y hacer scroll sin recargar todo el recurso.

## 9. Permiso desde un comentario con una proyección pequeña — P2

**Clase:** `CommentService`.
**Método:** `CheckResourcePermissionFromComment` y su consumidor `CheckResourcePermissionFromCommentId`.
**Línea actual:** [CommentService.cs:174](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:174).

```csharp
.Include(c => c.ResourceComment)
.FirstOrDefaultAsync(c => c.Id == commentId && c.ResourceComment != null, cancel)
```

**Datos necesarios:** `Id`, `UserId`, `ScoreId`, `Deleted`, `ResourceId`. No texto ni navegación materializada.

### Fix propuesto

```csharp
private sealed record CommentAccessRow(
    long Id, string UserId, long ScoreId, bool Deleted, long ResourceId);

private async Task<OneOf<CommentAccessRow, CommentServiceResult>>
    CheckResourcePermissionFromComment(long commentId, CancellationToken ct)
{
    var result = await _dbHelperService.TryFunction(
        cancel => _context.Comments.AsNoTracking()
            .Where(c => c.Id == commentId && c.ResourceComment != null)
            .Select(c => new CommentAccessRow(c.Id, c.UserId, c.ScoreId,
                c.Deleted, c.ResourceComment!.ResourceId))
            .FirstOrDefaultAsync(cancel),
        CommentServiceResult.DBError, ct: ct);
    if (!result.TryPickT0(out var comment, out var error))
        return error;
    if (comment is null)
        return CommentServiceResult.CommentNotFound;
    var permission = await CheckResourcePermission(comment.ResourceId, ct);
    if (permission != CommentServiceResult.Success)
        return permission;
    return comment;
}
```

`CheckResourcePermissionFromCommentId` puede mantener su extracción del error, porque solo necesita saber si resultó válido. El código del punto 10 usa los escalares de `CommentAccessRow`.

**Contrato/frontend:** no cambia. **Alcance:** conservo la política actual de este helper: solo admite comentarios asociados a recursos. No estoy afirmando que todos tus comentarios tengan recurso. Para otros tipos de hilo debes decidir sus reglas de acceso y extender el helper con esas reglas; no asignarles un recurso ficticio ni autorizarlos automáticamente.

## 10. Like/dislike: cargar solo el voto que se modifica — P2

**Clase:** `CommentService`.
**Método:** `AddEditOrDeleteLike`.
**Línea actual:** [CommentService.cs:122](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:122).

```csharp
.Include(l => l.Score).ThenInclude(l => l!.Comment)
```

**Datos necesarios:** después del permiso del punto 9, `comment.ScoreId` y un único `LikeUsers` con tracking. No su `Score` o comentario asociado.

### Fix propuesto

Conservar validaciones, errores de comentario propio/borrado, asignación de Value y SaveChanges. Reemplazar la consulta de `likeRes` por:

```csharp
var likeRes = await _dbHelperService.TryFunction(
    cancel => _context.LikesUsers.FirstOrDefaultAsync(
        l => l.UserId == userId && l.ScoreId == comment.ScoreId, cancel),
    CommentServiceResult.DBError, ct: ct);
```

Esto funciona también si aplicas el punto 9: el nuevo tipo conserva `ScoreId`, `UserId` y `Deleted`.

Para una lectura pequeña de estadísticas tras el POST, un contrato posible es:

```csharp
public sealed record CommentVoteReadDto(
    long CommentId, int Likes, int Dislikes, bool? MyVote);

// Dentro de CommentService. Llamar después de comprobar permisos del comentario.
private Task<CommentVoteReadDto?> GetVoteStatsAsync(long commentId, CancellationToken ct)
{
    var userId = _accountService.GetUserID();
    return _context.Comments.AsNoTracking().Where(c => c.Id == commentId)
        .Select(c => new CommentVoteReadDto(c.Id,
            c.Score!.LikesUsers.Count(v => v.Value),
            c.Score.LikesUsers.Count(v => !v.Value),
            c.Score.LikesUsers.Where(v => userId != null && v.UserId == userId)
                .Select(v => (bool?)v.Value).FirstOrDefault()))
        .FirstOrDefaultAsync(ct);
}
```

**Contrato/frontend:** el primer cambio no exige cambiar la respuesta actual `LikeUsers`. Al remover, esa respuesta representa la fila eliminada, **no** el nuevo voto activo: el frontend debe poner `myVote = null`. Si acordamos el DTO de estadísticas, adaptaremos ese POST/GET en conjunto. Como en el punto 3, separar fallo de refresco de fallo del guardado ya confirmado.

## 11. Carga con seguimiento específica para editar recurso — P2

**Clase:** `ResourceService`.
**Método:** `Edit`.
**Línea actual:** [ResourceService.cs:187](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:187).

```csharp
var res = await Get(resourceId, true, ct);
var check = CheckResourceIsEditable(res!);
```

**Datos necesarios:** entidad `Resource` con tracking, relaciones que se agregan/quitan y carpeta base de archivos. No votantes, categorías, datos de Identity, descripciones de dependencias ni todo el historial de archivos.

### Fix propuesto

Añadir una carga exclusiva para escritura dentro del servicio:

```csharp
private async Task<OneOf<Resource, ResourceServiceResult>> LoadForEdit(
    long resourceId, CancellationToken ct)
{
    var accessResult = await GetAccessAsync(resourceId, ct);
    if (!accessResult.TryPickT0(out var access, out var error))
        return error;
    var permission = CheckEditAccess(access);
    if (permission != ResourceServiceResult.Success)
        return permission;

    var userId = _accountService.GetUserID();
    var result = await _dbHelperService.TryFunction(
        cancel => _context.Resources.AsTracking().AsSplitQuery()
            .Where(r => r.Id == resourceId && userId != null && (
                r.SubmitterId == userId ||
                r.ResourcesAuthors.Any(a => a.UserId == userId) ||
                r.ResourceVersions.Any(v => v.SubmitterUserId == userId)))
            .Include(r => r.ResourceResourceTypes)
            .Include(r => r.ResourceTags)
            .Include(r => r.ResourcePreviews)
            .Include(r => r.ResourcesAuthors)
            .Include(r => r.ResourceExternalAuthors)
            .Include(r => r.ResourceDependencies)
            .Include(r => r.ResourceExternalDependencies)
            .FirstOrDefaultAsync(cancel),
        ResourceServiceResult.DBError, ct: ct);
    if (!result.TryPickT0(out var resource, out error))
        return error;
    return resource is null ? ResourceServiceResult.ResourceNotFound : resource;
}
```

Reemplazar el bloque de `Get`/`CheckResourceIsEditable` de `Edit` por:

```csharp
var result = await LoadForEdit(resourceId, ct);
if (!result.TryPickT0(out var resource, out var error))
    return error;
```

La carga mantiene las entidades de asociación con tracking: tus asignaciones de colecciones siguen eliminando/agregando relaciones según su configuración. **No** reemplazarla por un DTO ni ejecutar `Update` sobre un grafo leído sin tracking.

La carpeta base ya no depende de cargar todas las versiones y sus archivos. Añadir este helper, compartido por los puntos 11 y 12:

```csharp
private async Task<OneOf<string, ResourceServiceResult>> GetMainResourceFolderAsync(
    long resourceId, CancellationToken ct)
{
    var result = await _dbHelperService.TryFunction(
        cancel => _context.ResourceVersions.AsNoTracking()
            .Where(v => v.ResourceId == resourceId && v.File != null)
            .OrderBy(v => v.CreatedAt).ThenBy(v => v.Id)
            .Select(v => v.File!.URL).FirstOrDefaultAsync(cancel),
        ResourceServiceResult.DBError, ct: ct);
    if (!result.TryPickT0(out var url, out var error))
        return error;
    if (string.IsNullOrWhiteSpace(url))
        return ResourceServiceResult.FileNotFound;
    var segments = url.Replace('\\', '/').Split('/');
    // Formato actual: Storage/Resources/{carpeta-recurso}/{version}/{archivo}.
    if (segments.Length < 4 || segments.Take(3).Any(string.IsNullOrWhiteSpace))
        return ResourceServiceResult.FileNotFound;
    return string.Join('/', segments.Take(3));
}
```

En `Edit`, reemplazar `GetMainResourceFolder(resource)` por:

```csharp
var folderResult = await GetMainResourceFolderAsync(resourceId, ct);
if (!folderResult.TryPickT0(out var mainFolder, out var folderError))
    return folderError;
// A partir de aquí conservar BeginBatch, preparación, cambios y guardado.
```

**Corrección funcional encontrada en este bloque, distinta del rendimiento:** hoy `PrepareDeletion(removePrs.Select(pr => pr.Id), ...)` pasa IDs de **ResourcePreview**, pero `PrepareDeletion` busca por **File.Id**. Al revisar este método, cambiarlo a `removePrs.Select(pr => pr.FileId)`. Además, limitar la consulta de previews eliminadas por `pr.ResourceId == resourceId`; no aceptar IDs de previews de otros recursos. Puedes conservar la consulta actual con esos ajustes o capturar los `FileId` desde la colección cargada antes de quitar relaciones.

**Contrato/frontend:** no cambia el DTO de edición ni el resultado enum. Si aplicas el detalle DTO del punto 6, el editor necesita adaptarse para su **lectura inicial**, pero sus POST/PUT siguen enviando `ResourceEditDTO`.
**Resultado esperado:** el PUT carga asociaciones necesarias para cambiarlas, no todos los datos de presentación. Un número de consultas dividido no es por sí solo una regresión: comparar filas/materialización y tiempo total.

## 12. Añadir versión sin cargar todo el historial — P2

**Clase:** `ResourceService`.
**Método:** `AddVersion`.
**Línea actual:** [ResourceService.cs:351](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:351).

```csharp
.Include(r => r.ResourceVersions).ThenInclude(r => r.File)
.Include(r => r.ResourcesAuthors)
```

**Datos necesarios:** permiso de edición, si ya existe el nombre de versión y una URL para obtener la carpeta base. Para insertar basta `ResourceId` y el nuevo `File`.

### Fix propuesto: reemplazo completo

Depende de los helpers de acceso y carpeta base.

```csharp
public async Task<OneOf<ResourceVersion, ResourceServiceResult>> AddVersion(
    long resourceId, ResourceVersionCreateDTO dto, CancellationToken ct = default)
{
    if (dto.File.Length > FILE_MAX_SIZE)
        return ResourceServiceResult.FileSizeSurpassTheLimit;
    var userId = _accountService.GetUserID();
    if (userId is null)
        return ResourceServiceResult.Unauthorized;
    var accessResult = await GetAccessAsync(resourceId, ct);
    if (!accessResult.TryPickT0(out var access, out var error))
        return error;
    var permission = CheckEditAccess(access);
    if (permission != ResourceServiceResult.Success)
        return permission;

    var versionName = GetFullVersion(dto.Version);
    var duplicateResult = await _dbHelperService.TryFunction(
        cancel => _context.ResourceVersions.AnyAsync(
            v => v.ResourceId == resourceId && v.Version == versionName, cancel),
        ResourceServiceResult.DBError, ct: ct);
    if (!duplicateResult.TryPickT0(out var exists, out error))
        return error;
    if (exists)
        return ResourceServiceResult.VersionAlreadyExists;

    var folderResult = await GetMainResourceFolderAsync(resourceId, ct);
    if (!folderResult.TryPickT0(out var mainFolder, out error))
        return error;
    var batchPath = await _fileService.BeginBatch(ct);
    if (batchPath is null)
        return ResourceServiceResult.BatchCreationError;

    var path = GetVersionPath(dto.File.FileName,
        SanitizeFolderName(versionName), mainFolder);
    var file = await _fileService.PrepareFile(dto.File, path, batchPath, ct);
    if (file is null)
    {
        await _fileService.BatchRollback(batchPath, ct);
        return ResourceServiceResult.FileCreationError;
    }
    var version = new ResourceVersion
    {
        Id = 0, ResourceId = resourceId, FileId = 0, File = file,
        SubmitterUserId = userId, ChangeLog = dto.ReleaseNotes,
        Version = versionName, CreatedAt = DateTime.UtcNow,
        SealOfQualityRequested = dto.SealOfQualityRequested
    };
    _context.ResourceVersions.Add(version);
    if (!await _dbHelperService.TrySyncContext(ct))
    {
        await _fileService.BatchRollback(batchPath, ct);
        return ResourceServiceResult.DBError;
    }
    // El guardado ya se confirmó. Completar el manifiesto no debe revertir archivos.
    _fileService.BatchComplete(batchPath);
    return version;
}
```

**Dos precisiones:** comparo el nombre normalizado (`GetFullVersion`) en vez del input sin normalizar; evita consultar `null` y guardar después `1.0`. También asigno `CreatedAt`, que no se asigna en el constructor actual de este método; si prefieres generarlo en SQL, conserva ese mecanismo y verifica que se devuelve el valor generado. Estos ajustes son visibles para que puedas decidirlos.

**Contrato/frontend:** el servicio puede seguir devolviendo la entidad nueva para su controlador; punto 14 recorta el JSON. No cargar `Resource` para rellenar su navegación: el FK ya identifica el recurso existente.
**Límite:** `Any` no evita la carrera de dos solicitudes con el mismo nombre; una restricción única `(ResourceId, Version)` sería la garantía si deseas nombres únicos. `BatchComplete` actualmente captura sus propios fallos de limpieza; conservo ese comportamiento. La limpieza se realiza después del guardado confirmado y no llama rollback sobre archivos ya vinculados.

## 13. Editar una sola versión con tracking — P2

**Clase:** `ResourceService`.
**Método:** `EditVersion`.
**Línea actual:** [ResourceService.cs:409](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:409).

```csharp
.Include(r => r.ResourceVersions)
.Include(r => r.ResourcesAuthors)
```

**Datos necesarios:** permiso y la única versión cuyo `Id` y `ResourceId` coinciden. No todas las versiones.

### Fix propuesto: reemplazo completo

```csharp
public async Task<ResourceServiceResult> EditVersion(long resourceId,
    long versionId, ResourceVersionEditDTO dto, CancellationToken ct = default)
{
    var accessResult = await GetAccessAsync(resourceId, ct);
    if (!accessResult.TryPickT0(out var access, out var error))
        return error;
    var permission = CheckEditAccess(access);
    if (permission != ResourceServiceResult.Success)
        return permission;
    var result = await _dbHelperService.TryFunction(
        cancel => _context.ResourceVersions.AsTracking().FirstOrDefaultAsync(
            v => v.Id == versionId && v.ResourceId == resourceId, cancel),
        ResourceServiceResult.DBError, ct: ct);
    if (!result.TryPickT0(out var version, out error))
        return error;
    if (version is null)
        return ResourceServiceResult.VersionNotFound;

    version.ChangeLog = _dbHelperService.GetEditString(dto.ReleaseNotes, version.ChangeLog);
    version.Version = _dbHelperService.GetEditString(dto.Version, version.Version);
    version.UpdatedAt = DateTime.UtcNow;
    version.AlreadyModerated = false;
    version.SealOfQualityRequested = version.SealOfQuality ||
        (dto.SealOfQualityRequested ?? version.SealOfQualityRequested);
    return await _dbHelperService.TrySyncContext(ct)
        ? ResourceServiceResult.Success : ResourceServiceResult.DBError;
}
```

**Contrato/frontend:** no cambia. Conservo tu regla actual para `SealOfQualityRequested` y el significado de null/string vacío de `GetEditString`. `AsTracking` es intencional: no quitarlo por analogía con las lecturas.

## 14. Respuesta mínima después de crear una versión — P2

**Clase:** `ResourceController`.
**Método:** `CreateVersion`.
**Línea actual:** [ResourceController.cs:159](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Controllers/ResourceController.cs:159).

```csharp
return Ok(response);
```

**Datos necesarios:** ID de nueva versión y recurso para navegación. No `ResourceVersion.Resource` con el recurso y su historial.

### Fix propuesto

Conservar atributos de autorización, ruta y límites. Reemplazar firma/cuerpo:

```csharp
public async Task<ActionResult<OneOf<VersionCreatedDto, ResourceServiceResult>>>
    CreateVersion([FromRoute] long resourceId,
        [FromForm] ResourceVersionCreateDTO dto, CancellationToken ct)
{
    var result = await _resourceService.AddVersion(resourceId, dto, ct);
    if (result.TryPickT0(out var version, out var error))
        return Ok(new VersionCreatedDto(version.Id, version.ResourceId));
    return GetActionResult<VersionCreatedDto>(error);
}
```

**Contrato/frontend:** `{ "id": ..., "resourceId": ... }`. Si la vista vuelve al detalle tras guardar, basta este resultado. No hace falta incluir changelog, archivo ni relaciones de usuario para confirmar el guardado.

## 15. Búsqueda de recursos: solo Id y Name — P2

**Clase:** `ResourceService`; consumidor `ResourceController.GetResourcesLike`.
**Método:** `GetResourceLike`.
**Línea actual:** [ResourceService.cs:73](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:73).

```csharp
return await query.OrderBy(v => v.Name).Take(maxAmount).ToListAsync(ct);
```

**Datos necesarios:** `Id` y `Name` en el picker de dependencias. No descripción, flags ni otros campos de Resource.

### Fix propuesto

Cambiar el retorno del servicio a `Task<List<ResourceChoiceDto>>`, conservar su construcción de `query`, filtros de visibilidad y `Like`, y reemplazar el final:

```csharp
// 100 es un tope propuesto para un picker; puedes elegir otro.
var limit = Math.Clamp(maxAmount, 1, 100);
return await query.OrderBy(r => r.Name).ThenBy(r => r.Id)
    .Take(limit).Select(r => new ResourceChoiceDto(r.Id, r.Name))
    .ToListAsync(ct);
```

Cambiar la firma de la acción a `ActionResult<List<ResourceChoiceDto>>`. Mantener su ruta `api/resource/search`.

**Contrato/frontend:** los campos `id` y `name` ya son los que necesita el picker. No convertir esta búsqueda en el futuro listado público: tu búsqueda actualmente admite no listados y algunos privados. Tampoco ampliar silenciosamente los permisos aquí: tu filtro actual no contempla moderadores o submitters de versiones; si quieres igualarlo al GET, sería una decisión explícita de política, independiente de la proyección.
**Resultado esperado:** transferir los nombres encontrados, no las descripciones completas de hasta `maxAmount` recursos.

## 16. Búsqueda de usuarios sin Avatar ni ApplicationUser — P2

**Clase:** `UserService`.
**Método:** `GetUserLike`.
**Línea actual:** [UserService.cs:33](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/UserService.cs:33); Includes en líneas 14–15.

```csharp
.Include(ui => ui.Avatar).Include(ui => ui.User)
// ToListAsync, luego Select(UserNameDTO.FromUserInfo)
```

**Datos necesarios:** ID de usuario y username; opcionalmente color. Este picker no necesita email ni avatar.

### Fix propuesto

Conservar el DTO actual `UserNameDTO`. Construir la consulta sin Includes:

```csharp
IQueryable<UserInfo> query = _context.UserInfos.AsNoTracking();
// Conservar el filtro EF.Functions.Like(query.User.UserName...) actual.
```

Reemplazar materialización y conversión final por:

```csharp
var limit = Math.Clamp(maxAmount, 1, 100);
return await query.OrderBy(ui => ui.User!.UserName).ThenBy(ui => ui.UserId)
    .Take(limit).Select(ui => new UserNameDTO
    {
        Id = ui.UserId, Username = ui.User!.UserName!,
        UsernameColor = ui.UsernameColor
        // Email y Avatar permanecen null; no se consultan.
    }).ToListAsync(ct);
```

**Contrato/frontend:** sigue siendo `UserNameDTO`; no cambia la ruta. El acceso a `ui.User.UserName` en `Where`/`Select` se traduce a SQL sin `Include`. No aplicar esta reducción al resumen de autor de comentarios: allí sí quieres avatar.

## 17. Validar tipos en una consulta por conjunto — P2

**Clase:** `ResourceService`.
**Método:** `GetTypes`.
**Línea actual:** [ResourceService.cs:778](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:778).

```csharp
foreach (var typeid in typeIds)
    await _context.ResourceTypes.FirstOrDefaultAsync(r => r.Id == typeid, ct);
```

**Datos necesarios:** IDs existentes y asociaciones nuevas por FK. No entidades ResourceType completas.

### Fix propuesto: reemplazo completo manteniendo la lista nullable del caller

```csharp
private async Task<List<ResourceResourceTypes?>> GetTypes(
    long[] typeIds, CancellationToken ct)
{
    var requested = typeIds.Distinct().ToArray();
    if (requested.Length == 0)
        return [];
    var existing = (await _context.ResourceTypes.AsNoTracking()
        .Where(t => requested.Contains(t.Id)).Select(t => t.Id)
        .ToListAsync(ct)).ToHashSet();
    return requested.Select(id => existing.Contains(id)
        ? new ResourceResourceTypes { Id = 0, ResourceId = 0, ResourceTypeId = id }
        : (ResourceResourceTypes?)null).ToList();
}
```

**Contrato/frontend:** no cambia. Tus callers siguen haciendo `types.Any(t => t == null)` y devolviendo `ResourceTypeNotFound`. La asociación se enlaza al nuevo/existente recurso por la colección como actualmente.
**Resultado esperado:** N consultas se convierten en una cuando hay N tipos.

## 18. Validar dependencias en una consulta por conjunto — P2

**Clase:** `ResourceService`.
**Método:** `GetDependencies`.
**Línea actual:** [ResourceService.cs:798](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:798).

```csharp
await _context.Resources.FirstOrDefaultAsync(r => r.Id == dependencyId, ct);
```

**Datos necesarios:** existencia de IDs. Si decides validar permisos de cada dependencia al guardar, calcular ese booleano en SQL, no cargar sus relaciones.

### Fix propuesto: reemplazo completo conservando la validación actual de existencia

```csharp
private async Task<List<ResourceDependency?>> GetDependencies(
    long[] dependencyIds, CancellationToken ct)
{
    var requested = dependencyIds.Distinct().ToArray();
    if (requested.Length == 0)
        return [];
    var existing = (await _context.Resources.AsNoTracking()
        .Where(r => requested.Contains(r.Id)).Select(r => r.Id)
        .ToListAsync(ct)).ToHashSet();
    return requested.Select(id => existing.Contains(id)
        ? new ResourceDependency { Id = 0, ResourceId = 0, DependencyResourceId = id }
        : (ResourceDependency?)null).ToList();
}
```

**Contrato/frontend:** no cambia. Conservo el comportamiento actual: el helper comprueba existencia, no visibilidad. Si agregas autorización aquí, no ocultar un fallo de DB como recurso inexistente. Rechazar autodependencia/ciclos también sería validación funcional adicional, no una consecuencia automática del batch.

## 19. Validar autores en una consulta por conjunto — P2

**Clase:** `ResourceService`.
**Método:** `GetAuthors`.
**Línea actual:** [ResourceService.cs:830](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:830).

```csharp
await _context.Users.FirstOrDefaultAsync(u => u.Id == authorId, ct);
```

**Datos necesarios:** IDs existentes; la entidad `ApplicationUser` no se necesita para insertar `ResourceAuthors`.

### Fix propuesto: reemplazo completo

```csharp
private async Task<List<ResourceAuthors?>> GetAuthors(string[] authorIDs,
    bool iAmAuthor, string userId, CancellationToken ct)
{
    // Conservar IAmAuthor. En Edit lo llamas con true: no cambio esa política.
    var requested = (iAmAuthor ? authorIDs.Append(userId) : authorIDs)
        .Distinct(StringComparer.Ordinal).ToArray();
    if (requested.Length == 0)
        return [];
    var existing = (await _context.Users.AsNoTracking()
        .Where(u => requested.Contains(u.Id)).Select(u => u.Id)
        .ToListAsync(ct)).ToHashSet(StringComparer.Ordinal);
    return requested.Select(id => existing.Contains(id)
        ? new ResourceAuthors { Id = 0, ResourceId = 0, UserId = id }
        : (ResourceAuthors?)null).ToList();
}
```

**Contrato/frontend:** no cambia. No asigno `User = authorEntity`: `UserId` basta para una relación con usuario existente. Esto no cambia el resumen del GET de lectura, que se proyecta independientemente. Aquí también valido existencia del submitter cuando se agrega vía `iAmAuthor`; si el claim apunta a un usuario ya eliminado, se produce el error de usuario inexistente.

## 20. Buscar todos los tags requeridos juntos — P2

**Clase:** `ResourceService`.
**Método:** `GetTags`.
**Línea actual:** [ResourceService.cs:868](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:868).

```csharp
await _context.Tags.FirstOrDefaultAsync(
    t => t.Name.ToLower().Trim() == tag, ct);
```

**Datos necesarios:** tags existentes correspondientes a los nombres normalizados, y entidades nuevas para los restantes. No una consulta por tag.

### Fix propuesto: reemplazo completo

```csharp
private async Task<List<ResourceTags>> GetTags(string[] tagNames, CancellationToken ct)
{
    var names = tagNames.Where(t => !string.IsNullOrWhiteSpace(t))
        .Select(t => t.ToLower().Trim()).Distinct().ToArray();
    if (names.Length == 0)
        return [];

    // Tag tiene Id/Name: aquí materializar la entidad es útil para enlazarla.
    // Con tracking para reutilizar la misma instancia si ya está en el contexto.
    var existing = await _context.Tags.AsTracking()
        .Where(t => names.Contains(t.Name.ToLower().Trim()))
        .OrderBy(t => t.Id).ToListAsync(ct);
    var byName = existing.GroupBy(t => t.Name.ToLower().Trim())
        .ToDictionary(g => g.Key, g => g.First());

    List<ResourceTags> result = [];
    foreach (var name in names)
    {
        if (!byName.TryGetValue(name, out var tag))
        {
            tag = new Tag
            {
                Id = 0, Name = char.ToUpperInvariant(name[0]) + name[1..]
            };
            byName.Add(name, tag);
        }
        result.Add(new ResourceTags { ResourceId = 0, TagId = tag.Id, Tag = tag });
    }
    return result;
}
```

**Contrato/frontend:** no cambia. Los tags nuevos se conservan por su instancia/nombre, no se deduplican todos por `Id == 0`. No hay `SaveChanges` dentro del helper: el grafo sigue guardándose al final.
**Precisión:** conservo tu normalización actual. Si ya existen dos tags equivalentes al normalizar, selecciono el de menor ID; este código no fusiona sus relaciones existentes. La collation SQL y la cultura de `ToLower` en C# pueden diferir; decidir una normalización persistida/indexada sería otro paso. Si la columna aún fuera SQL `text`, corregir primero su tipo: el batch no hace comparables tipos no compatibles.
**Resultado esperado:** una lectura para todos los nombres. `LOWER(TRIM(...))` puede seguir impidiendo un seek eficiente sobre un índice normal; elimina viajes repetidos, no todos los costes de búsqueda.

## 21. Limitar recomendaciones de autores externos incluso sin búsqueda — P2

**Clase:** `ExternalAuthorService`.
**Método:** `GetAuthorLike`.
**Línea actual:** [ExternalAuthorService.cs:14](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ExternalAuthorService.cs:14).

```csharp
return await query.Select(v => v.Author).Distinct().Order().ToListAsync(ct);
```

**Datos necesarios:** hasta `maxAmount` nombres distintos, también cuando el texto de búsqueda está vacío o tiene una letra. No todos los nombres históricos.

### Fix propuesto: reemplazo completo

```csharp
public async Task<List<string>> GetAuthorLike(string likeString,
    int maxAmount = 10, CancellationToken ct = default)
{
    var term = likeString?.ToLower().Trim() ?? "";
    var limit = Math.Clamp(maxAmount, 1, 100);
    var query = _context.ResourcesExternalAuthors.AsNoTracking();
    if (term.Length >= 2)
    {
        var escaped = term.Replace(@"\", @"\\").Replace("%", @"\%")
            .Replace("_", @"\_").Replace("[", @"\[");
        var pattern = $"%{escaped}%";
        query = query.Where(a => EF.Functions.Like(a.Author, pattern, @"\"));
    }
    return await query.Select(a => a.Author).Distinct().Order()
        .Take(limit).ToListAsync(ct);
}
```

**Contrato/frontend:** no cambia, sigue siendo `List<string>`. El usuario puede seguir escribir un nombre libre; las recomendaciones son acotadas. `Distinct` ordena/compara en SQL y requiere una columna comparable, como ya corregiste anteriormente.

## 22. Publicar respuestas sin cargar el comentario padre completo y Resource — P3

**Clase:** `CommentService`.
**Método:** `AddCommentToComment`.
**Línea actual:** [CommentService.cs:300](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:300).

```csharp
.Include(c => c.ResourceComment).ThenInclude(c => c!.Resource)
```

**Datos necesarios:** existencia del padre, `Deleted` y `ResourceId` opcional; permiso si pertenece a un recurso; resumen del autor de la respuesta. El JSON no necesita volver a devolver el comentario padre ni el recurso.

### Fix propuesto: reemplazo completo

```csharp
public async Task<OneOf<ReplyCreatedDto, CommentServiceResult>> AddCommentToComment(
    long commentId, string commentText, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(commentText))
        return CommentServiceResult.EmptyComment;
    var stateResult = await _dbHelperService.TryFunction(
        cancel => _context.Comments.AsNoTracking().Where(c => c.Id == commentId)
            .Select(c => new
            {
                c.Id, c.Deleted,
                ResourceId = c.ResourceComment == null
                    ? (long?)null : c.ResourceComment.ResourceId
            }).FirstOrDefaultAsync(cancel),
        CommentServiceResult.DBError, ct: ct);
    if (!stateResult.TryPickT0(out var parent, out var error))
        return error;
    if (parent is null)
        return CommentServiceResult.CommentNotFound;
    var userId = _accountService.GetUserID();
    if (userId is null)
        return CommentServiceResult.Unauthorized;
    if (parent.Deleted)
        return CommentServiceResult.CantReplyDeletedComment;
    if (parent.ResourceId is long resourceId)
    {
        var permission = await CheckResourcePermission(resourceId, ct);
        if (permission != CommentServiceResult.Success)
            return permission;
    }
    // Se conserva tu comportamiento para padres sin ResourceComment.
    var summaryResult = await _accountService.GetUserSummary(userId, ct);
    if (!summaryResult.TryPickT0(out var summary, out var summaryError))
        return summaryError == AccountServiceResult.UserNotFound
            ? CommentServiceResult.Unauthorized : CommentServiceResult.DBError;

    var child = new Comment
    {
        UserId = userId, Text = commentText,
        PublishDate = DateTime.UtcNow, Score = new Score()
    };
    if (parent.ResourceId is long targetResourceId)
        child.ResourceComment = new ResourceComments
        {
            Id = 0, CommentId = 0, ResourceId = targetResourceId
        };
    _context.CommentComments.Add(new CommentComment
    {
        Id = 0, ParentCommentId = commentId, ChildCommentId = 0,
        ChildComment = child
    });
    if (!await _dbHelperService.TrySyncContext(ct))
        return CommentServiceResult.DBError;
    return new ReplyCreatedDto(commentId, new CommentReadDto
    {
        Id = child.Id, UserId = userId, ScoreId = child.ScoreId,
        Text = child.Text, PublishDate = child.PublishDate,
        UserSummary = summary, HasReplies = false, Likes = 0, Dislikes = 0,
        MyVote = null, Deleted = false
    });
}
```

**Contrato/frontend:** adaptar la firma de la acción/helper genérico y el adaptador de respuesta: ahora `parentCommentId` + `comment`, no un grafo `CommentComment` con `parentComment`/`childComment`. Al insertar el resultado, marcar `hasReplies` del padre y mantener el comportamiento de scroll actual.
**Política:** este método conserva tu aceptación de respuestas a comentarios no vinculados a recursos. Si son de perfiles u otro subsistema, su autorización adicional debe venir de ese subsistema; no asumir visibilidad de un recurso inexistente. El helper del punto 9, en cambio, conserva el alcance de comentarios de recursos que tiene hoy.

## 23. Comprobar hijos sin materializarlos al borrar — P3

**Clase:** `CommentService`.
**Método:** `DeleteComment`.
**Línea actual:** [CommentService.cs:393](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:393).

```csharp
.Include(c => c.Childrens)
// Luego: if (comment.Childrens.Count <= 0)
```

**Datos necesarios:** comentario con tracking y `HasReplies`. Mantener `Parent` y `ResourceComment`, porque sus relaciones tienen `ClientCascade` y deben estar cargadas para eliminarlas junto al comentario.

### Fix propuesto

Eliminar **solo** `.Include(c => c.Childrens)` de la consulta actual. Mantener:

```csharp
.Include(c => c.Parent)
.Include(c => c.ResourceComment)
```

Después de tus comprobaciones de existencia y autorización, reemplazar el bloque que decide borrar/marcar por:

```csharp
var childrenResult = await _dbHelperService.TryFunction(
    cancel => _context.CommentComments.AnyAsync(
        link => link.ParentCommentId == commentId, cancel),
    CommentServiceResult.DBError, ct: ct);
if (!childrenResult.TryPickT0(out var hasReplies, out var error))
    return error;
if (!hasReplies)
    _context.Comments.Remove(comment);
else
{
    comment.Deleted = true;
    comment.Text = "";
    comment.UpdatedAt = DateTime.UtcNow;
}
// Conservar TrySyncContext y el enum final del método.
```

**Contrato/frontend:** no cambia. Ocultar un borrado sin hijos en la lectura ya está contemplado en el punto 2. `Any` agrega una consulta pequeña; el beneficio es no materializar una cantidad arbitraria de enlaces de respuestas.
**Precisión de concurrencia:** otra respuesta podría insertarse entre `Any` y el borrado. Eso ya puede ocurrir entre el Include actual y el SaveChanges; esta optimización no lo resuelve. Si necesitas garantizar la regla bajo concurrencia, coordinar borrado y publicación con una transacción/nivel de aislamiento o una operación SQL que aplique la condición atómicamente. También el cascade actual de `Comment.Score` no significa que borrar Comment borre automáticamente el Score principal: su limpieza es otra decisión de integridad.

## 24. Perfil privado proyectado, sin materializar ApplicationUser — P3

**Clase:** `AccountService`.
**Método actual:** `GetUserInfo(string userId, ...)`; propuesto para la vista propia `GetMyProfileRead`.
**Línea actual:** [AccountService.cs:138](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/AccountService.cs:138).

```csharp
.Include(info => info.User)
```

**Datos necesarios:** datos de perfil/editables, username, email privado de la cuenta, avatar, timezone y visibilidad del email. No toda la entidad Identity. No recortar redes sociales ni campos usados en el editor.

### Fix propuesto

DTO específico para **el perfil del propio usuario**. No reutilizarlo en un endpoint público de otros perfiles, porque incluye email y cumpleaños.

```csharp
public sealed record ProfileOptionReadDto(long Id, string? Name);
public sealed record TimeZoneReadDto(long Id, string Name, int Value);
public sealed class ProfileReadDto
{
    public long Id { get; init; }
    public required string UserId { get; init; }
    public string? UsernameColor { get; init; }
    public string? Title { get; init; }
    public long AvatarId { get; init; }
    public DateOnly Birthday { get; init; }
    public string? Bio { get; init; }
    public string? Patreon { get; init; }
    public string? Paypal { get; init; }
    public string? BuyMeACoffee { get; init; }
    public string? Discord { get; init; }
    public string? Twitter { get; init; }
    public string? Facebook { get; init; }
    public string? Youtube { get; init; }
    public string? Instagram { get; init; }
    public string? Steam { get; init; }
    public string? Twitch { get; init; }
    public string? Github { get; init; }
    public string? NintendoNetwork { get; init; }
    public long EmailVisibilityId { get; init; }
    public long TimeZoneId { get; init; }
    public UserNameDTO? UserSummary { get; init; }
    public FileResponse? Avatar { get; init; }
    public ProfileOptionReadDto? EmailVisibility { get; init; }
    public TimeZoneReadDto? TimeZone { get; init; }
}
```

Método nuevo dentro de `AccountService`; conserva la distinción de cancelación que ya implementaste:

```csharp
public async Task<OneOf<ProfileReadDto, AccountServiceResult>> GetMyProfileRead(
    CancellationToken ct = default)
{
    var userId = GetUserID();
    if (userId is null)
        return AccountServiceResult.UserNotFound;
    try
    {
        var profile = await _context.UserInfos.AsNoTracking()
            .Where(i => i.UserId == userId)
            .Select(i => new ProfileReadDto
            {
                Id = i.Id, UserId = i.UserId, UsernameColor = i.UsernameColor,
                Title = i.Title, AvatarId = i.AvatarId,
                Birthday = i.Birthday, Bio = i.Bio,
                Patreon = i.Patreon, Paypal = i.Paypal, BuyMeACoffee = i.BuyMeACoffee,
                Discord = i.Discord, Twitter = i.Twitter, Facebook = i.Facebook,
                Youtube = i.Youtube, Instagram = i.Instagram, Steam = i.Steam,
                Twitch = i.Twitch, Github = i.Github, NintendoNetwork = i.NintendoNetwork,
                EmailVisibilityId = i.EmailVisibilityId, TimeZoneId = i.TimeZoneId,
                UserSummary = new UserNameDTO
                {
                    Id = i.UserId, Username = i.User!.UserName!,
                    UsernameColor = i.UsernameColor, Email = i.User.Email
                },
                Avatar = i.Avatar == null ? null : new FileResponse
                {
                    Id = i.Avatar.Id, URL = i.Avatar.URL,
                    FileTypeId = i.Avatar.FileTypeId
                },
                EmailVisibility = i.EmailVisibility == null ? null
                    : new ProfileOptionReadDto(i.EmailVisibility.Id, i.EmailVisibility.Name),
                TimeZone = i.TimeZone == null ? null
                    : new TimeZoneReadDto(i.TimeZone.Id, i.TimeZone.Name, i.TimeZone.Value)
            }).FirstOrDefaultAsync(ct);
        return profile is null ? AccountServiceResult.UserNotFound : profile;
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        return AccountServiceResult.CancelledToken;
    }
    catch
    {
        return AccountServiceResult.Error;
    }
}
```

**Contrato/frontend:** `ProfileController.GetMyProfile` llama este método y actualiza su firma/`ProducesResponseType` a `ProfileReadDto`, conservando su mapeo actual de 404/499/error. La estructura de campos queda parecida a `UserInfo`; cambiar el tipo de deserialización en perfil/editor y mantener avatar/color/email. No se cambia `EditProfileDTO` ni la semántica null/string vacío al guardar.

**Alcance:** no reemplazar de forma automática todas las llamadas a `GetUserInfo(string)`: otros callers podrían consultar un perfil ajeno y necesitar otras reglas de privacidad. Esta propuesta separa explícitamente el caso privado `/api/profile/me`.

## 25. Endpoint mínimo de sesión en lugar de usar el perfil completo — P3

**Clase:** `ProfileController`, con método en `AccountService`.
**Método actual:** `GetMyProfile`, usado también para verificar acceso en frontend.
**Línea actual:** [ProfileController.cs:21](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Controllers/ProfileController.cs:21).

```csharp
var result = await _accountService.GetUserInfo(ct);
```

**Datos necesarios para navegación/guard:** ID, username, usernameColor, avatar y, si se necesita, condición de staff. No bio, cumpleaños, email o redes sociales. **El perfil/editor sí necesitan la respuesta completa** del punto 24.

### Fix propuesto: endpoint adicional, no cambio destructivo de /me

```csharp
// DTO nuevo; no contiene permisos de edición de un recurso específico.
public sealed record SessionReadDto(UserNameDTO UserSummary, bool IsStaff);

// AccountService; depende de GetUserSummary del punto 26.
public async Task<OneOf<SessionReadDto, AccountServiceResult>> GetMySession(
    CancellationToken ct = default)
{
    var userId = GetUserID();
    if (userId is null)
        return AccountServiceResult.UserNotFound;
    var result = await GetUserSummary(userId, ct);
    if (!result.TryPickT0(out var summary, out var error))
        return error;
    return new SessionReadDto(summary, IsModeratorOrAdmin());
}

// ProfileController: conservar su [Route("api/profile")].
[HttpGet("me/session")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public async Task<ActionResult<SessionReadDto>> GetMySession(CancellationToken ct)
{
    var result = await _accountService.GetMySession(ct);
    if (result.TryPickT0(out var session, out var error))
        return Ok(session);
    return error switch
    {
        AccountServiceResult.UserNotFound => NotFound(),
        AccountServiceResult.CancelledToken => StatusCode(StatusCodes.Status499ClientClosedRequest),
        _ => BadRequest()
    };
}
```

**Contrato/frontend:** cambiar el módulo compartido `account-session.js` y sus adaptadores a `/api/profile/me/session`; no hacer dos verificaciones en paralelo ni usar el endpoint completo también. `profile.js` mantiene `/api/profile/me` para la pantalla de perfil. Conservar invalidación al editar avatar/color y al login/logout, y el cache pequeño **en navegador** que ya implementamos.

**Prioridad:** opcional después de los principales. La deduplicación/cache del navegador del punto 3 del diagnóstico general ya reduce solicitudes repetidas. Este cambio reduce el coste de las que todavía ocurren; no introduce cache del servidor ni evita validar identidad al autorizar un POST/PUT.

## 26. Resumen de usuario directo para autores de comentarios — P3

**Clase:** `AccountService`; consumidor `UserNameDTO.FromUser`/publicación de comentarios.
**Método actual:** `GetUser`.
**Línea actual:** [AccountService.cs:316](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/AccountService.cs:316).

```csharp
await _context.Users.FindAsync([userId], cancel)
```

**Datos necesarios para estos callers:** ID, username, color y avatar. `FindAsync` trae la entidad Identity y no garantiza que `UserInfo`/Avatar estén cargados para construir el resumen.

### Fix propuesto: método adicional, no reemplazar todas las lecturas Identity

```csharp
public async Task<OneOf<UserNameDTO, AccountServiceResult>> GetUserSummary(
    string userId, CancellationToken ct = default)
{
    try
    {
        var summary = await _context.Users.AsNoTracking()
            .Where(u => u.Id == userId).Select(u => new UserNameDTO
            {
                Id = u.Id, Username = u.UserName!,
                UsernameColor = u.UserInfo == null ? null : u.UserInfo.UsernameColor,
                Avatar = u.UserInfo == null || u.UserInfo.Avatar == null
                    ? null : new FileResponse
                    {
                        Id = u.UserInfo.Avatar.Id, URL = u.UserInfo.Avatar.URL,
                        FileTypeId = u.UserInfo.Avatar.FileTypeId
                    }
                // Email queda null: este resumen puede mostrarse públicamente.
            }).FirstOrDefaultAsync(ct);
        return summary is null ? AccountServiceResult.UserNotFound : summary;
    }
    catch (OperationCanceledException) when (ct.IsCancellationRequested)
    {
        return AccountServiceResult.CancelledToken;
    }
    catch
    {
        return AccountServiceResult.Error;
    }
}
```

Cambiar los callers de publicación a este método, como en los puntos 8 y 22. Conservar `GetUser` donde realmente se necesita una entidad para Identity/modificaciones; no optimizarlo globalmente por un único consumidor.
**Contrato/frontend:** los comentarios mantienen `UserNameDTO` con avatar y color. En el GET de detalle de recurso (punto 6) se proyecta el resumen **sin avatar**, respetando lo que pediste para ese GET.

## 27. Videojuegos: respuesta solo Id y Name — P3

**Clase:** `VideogameService`; consumidor `VideogameController`.
**Método:** `GetVideogamesLike`.
**Línea actual:** [VideogameService.cs:28](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/VideogameService.cs:28); rama inicial en línea 14.

```csharp
.ToListAsync(ct) // Sobre Videogame completo.
```

**Datos necesarios:** ID y nombre. Mantener `Any` (1), `Other` (2) al comenzar, y búsqueda remota con límite.

### Fix propuesto: reemplazo completo

Reutilizar `SelectOptionDto`, ya existente:

```csharp
public async Task<List<SelectOptionDto>> GetVideogamesLike(string likeString,
    int maxAmount = 10, CancellationToken ct = default)
{
    var term = likeString?.ToLower().Trim() ?? "";
    var limit = Math.Clamp(maxAmount, 1, 100);
    var query = _context.Videogames.AsNoTracking();
    if (term.Length < 2)
        return await query.Where(v => v.Id == 1 || v.Id == 2)
            .OrderBy(v => v.Id)
            .Select(v => new SelectOptionDto { Id = v.Id, Name = v.Name })
            .ToListAsync(ct);
    var escaped = term.Replace(@"\", @"\\").Replace("%", @"\%")
        .Replace("_", @"\_").Replace("[", @"\[");
    var pattern = $"%{escaped}%";
    return await query.Where(v => EF.Functions.Like(v.Name, pattern, @"\"))
        .OrderBy(v => v.Name).ThenBy(v => v.Id).Take(limit)
        .Select(v => new SelectOptionDto { Id = v.Id, Name = v.Name })
        .ToListAsync(ct);
}
```

**Contrato/frontend:** cambiar tipo del controlador a `List<SelectOptionDto>`; ruta y query params se conservan. El picker ya consume `id/name`; no cambiar el debounce ni cargar los 1.700 videojuegos en el formulario inicial.
**Precisión:** la rama inicial devuelve siempre los dos valores especiales, como hoy; no recorta `Other` aunque el caller pida `maxAmount = 1`.

## 28. Obtener solo el ID del MIME registrado al preparar archivo — P3

**Clase:** `FileService`.
**Método:** `PrepareFile`.
**Línea actual:** [FileService.cs:53](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/FileService.cs:53).

```csharp
type = await _context.FileTypes.FirstOrDefaultAsync(
    f => string.Compare(f.Value.ToLower(), filetype.ToLower().Trim()) == 0, ct);
```

**Datos necesarios:** ID del tipo de archivo, no la entidad completa. No confundir esta optimización con validar contenido/tamaño/admisibilidad por operación.

### Fix propuesto: reemplazo parcial

```csharp
var mime = fileRequest.ContentType.ToLower().Trim();
long? typeId = null;
try
{
    typeId = await _context.FileTypes.AsNoTracking()
        .Where(t => t.Value.ToLower() == mime)
        .Select(t => (long?)t.Id).FirstOrDefaultAsync(ct);
}
catch
{
    // Conservo tu contrato actual: el fallo de lookup termina en null + rollback.
}
if (typeId is null)
{
    await BatchRollback(batchPath, ct);
    return null;
}
// Conservar UpdateBatch, copia física y sus ramas de fallo actuales.
// En el constructor final de Models.File usar:
var file = new Models.File
{
    URL = destinationPath.Replace('\\', '/'),
    FileTypeId = typeId.Value
};
_context.Files.Add(file);
return file;
```

**Precisión:** la copia física va entre el chequeo de tipo y el constructor final, como hoy; no omitirla al pegar el bloque. No añadir `SaveChanges` en `FileService`.
**Contrato/frontend:** no cambia. El cast `(long?)` permite diferenciar “no encontré fila” de un ID igual a 0. La validación pendiente de archivos sigue siendo un tema separado que me pediste recordar cuando lo preguntes.
**Resultado esperado:** pequeña reducción por archivo; no soluciona por sí misma una subida lenta de 250 MB. Si todos los MIME de la tabla están normalizados y su collation sirve, podrías comparar `t.Value == mime` y evitar `LOWER` sobre la columna, pero requiere comprobar ese supuesto antes.

## 29. Año del usuario: consultar solo el offset horario — P3

**Clase:** `DateTimeService`.
**Método:** `GetYear`.
**Línea actual:** [DateTimeService.cs:12](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/DateTimeService.cs:12).

```csharp
.Include(info => info.TimeZone)
.FirstOrDefaultAsync(info => info.UserId == userId, ct)
```

**Datos necesarios:** `TimeZone.Value` nullable. No perfil, bio, enlaces ni toda la entidad TimeZone.

### Fix propuesto: reemplazo completo

```csharp
public async Task<int?> GetYear(string userId, CancellationToken ct = default)
{
    var offset = await _context.UserInfos.AsNoTracking()
        .Where(i => i.UserId == userId)
        .Select(i => i.TimeZone == null ? (int?)null : i.TimeZone.Value)
        .FirstOrDefaultAsync(ct);
    return offset is null ? null : DateTime.UtcNow.AddHours(offset.Value).Year;
}
```

**Contrato/frontend:** no cambia. Conserva null si no hay perfil/timezone y el cálculo con offset fijo que usas actualmente. Soportar horario de verano con una zona real requeriría un modelo distinto; no lo introduzco en este fix.

---

## Consumidores del frontend que habría que adaptar al aplicar los DTOs

Estos ajustes **no se hicieron como parte de este documento**. Su implementación corresponde al frontend cuando decidas qué contratos aplicar.

| Cambio aplicado | Consumidores a revisar | Ajuste |
|---|---|---|
| Detalle y Rating DTO (1, 6) | `Components/Pages/ResourceDetails.razor.cs`, su `.razor`, `ResourceRating`, `ResourceReleaseList`, `ResourceMediaCarousel`, `wwwroot/js/resource-details.js`, `Components/Resources/ResourceEditor.razor.cs`, `wwwroot/js/resource-editor.js`, `Components/Shared/PageAccessGuard.razor.cs`, `wwwroot/js/page-access.js` | Cambiar tipo/forma de deserialización y parámetros de componentes; leer rating y listas planas; conservar IDs de asociación para remover; usar CanEdit solo como presentación. |
| Comentario DTO (2, 8, 22) | `Components/Resources/CommentApiClient.cs`, `ResourceCommentsPanel`, `ResourceCommentList`, `ResourceCommentCard`, DTOs/adaptadores de comentarios, `wwwroot/js/comment-api.js` | Leer Likes/Dislikes/MyVote/HasReplies; aceptar respuestas de publicación compactas; conservar usernameColor/avatar, replies, infinite scroll y borrado. |
| Archivo DTO (4) | `resource-details.js`, helpers de archivos/previews y descargas | Leer MimeType; mantener endpoint de descarga y la forma actual de obtener contenido. No publicar Storage como directorio estático. |
| Resultado de creación (5, 14) | `CreateResource.razor.cs`, `ResourceEditor.razor.cs`, adaptadores JS de subida | Leer ID; no depender de entidad completa en éxito. |
| Pickers mínimos (15, 16, 27) | Pickers de dependencias, usuarios y videojuegos, `resource-editor.js` | Mantener id/name o id/username, límites y búsqueda remota; no asumir propiedades adicionales. |
| Perfil DTO (24) | Perfil/editor y `wwwroot/js/profile.js` | Deserializar ProfileReadDto conservando todas las redes y campos; no cambiar EditProfileDTO. |
| Sesión DTO (25) | `wwwroot/js/account-session.js`, navegación, PageAccessGuard, comprobación de sesión de detalle/editor/comentarios/CheckEmail | Cambiar endpoint compartido y adaptador; conservar deduplicación, TTL e invalidación en navegador. Perfil completo usa su propio GET. |

En los controladores, además de la firma, actualizar los `ProducesResponseType` existentes y el tipo genérico del helper de errores cuando cambie el tipo de éxito. No devolver el `OneOf` entero por accidente: seguir desempaquetando éxito/error como actualmente.

## Qué aplicaría primero

1. **5 y 14:** IDs como respuestas de creación; son pequeños y no alteran cómo guardas entidades.
2. **3 y 10:** cambiar consultas de votos conservando inicialmente respuestas actuales.
3. **7 y 9:** permisos por proyección; usar esos helpers sin materializar colecciones.
4. **4:** archivo individual con permiso y pertenencia.
5. **1 + 6 + 11:** separar lectura/escritura del recurso y coordinar DTO con vistas. Este grupo es el de mayor impacto potencial en el detalle.
6. **2 + 8 + 22 + 26:** contrato coherente para comentarios; además elimina lecturas por comentario de voto propio cuando la página ya lo trae.
7. **17–20:** batch de asociaciones; luego **12–13** para versiones.
8. **15–16, 21, 27:** recortar y limitar pickers.
9. **23–25, 28–29:** optimizaciones secundarias.

Este orden práctico coloca algunos cambios pequeños antes de otros de mayor impacto para que puedas aplicar mejoras sin una migración masiva de contratos. Las prioridades P1/P2/P3 describen impacto potencial, no una obligación de copiar en orden estricto.

## Límites de esta propuesta

- Reducir entidades/respuestas no garantiza solucionar los dos SQL de ~25 segundos del diagnóstico. El mismo SQL también duró 92 ms en el log: planes, parámetros, bloqueos y esperas necesitan observación en el servidor si la demora persiste.
- Una lista pequeña con `Contains` es adecuada para las selecciones de estos formularios. Si admitieras miles de IDs por request, habría que limitar o dividir el conjunto considerando la traducción del proveedor; no propongo pasar una lista ilimitada.
- Los ejemplos conservan tu `DBHelperService`. Actualmente algunos helpers atrapan cualquier excepción y pierden su detalle. El documento no incorpora una refactorización global de errores ni logging.
- `SaveChanges` confirmado y limpieza del manifiesto son eventos distintos. Actualmente `BatchComplete` captura internamente sus excepciones, por lo que un fallo de limpieza no sale hacia `TrySyncContextWithFiles`. Conservar esa separación si cambias el manejo de errores: **no borrar archivos ya referenciados por la DB** por un fallo posterior de limpieza. Mantener el manifiesto para mantenimiento es compatible con tu estrategia, siempre que no se eliminen archivos comprometidos.
- Reemplazar consultas de permisos por booleanos no resuelve cambios de permisos concurrentes durante una escritura. Mantener autorización del lado servidor; definir transacciones/concurrencia si necesitas garantías frente a esas carreras.
- La validación de MIME por operación, restricciones únicas y limpieza de datos históricos no se arreglan por usar DTOs. No presento estos cambios como solución de todos los problemas de integridad.

## Referencias técnicas

Las recomendaciones se apoyan en la revisión del código y en estas referencias oficiales:

- [EF Core: seleccionar solo las propiedades necesarias](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying#project-only-properties-you-need).
- [EF Core: evaluación en servidor y cliente](https://learn.microsoft.com/en-us/ef/core/querying/client-eval).
- [EF Core: consultas simples/divididas y sus costes](https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries).

No hay una medición de mejora ni una validación de compilación de estos ejemplos. Son una propuesta de implementación sobre el código leído hoy, para aplicar selectivamente y coordinar sus contratos con el frontend.

