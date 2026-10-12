# Consultas por conjunto en ResourceService

## Alcance

Propuesta para aplicar manualmente al backend. No se modificaron servicios,
controladores, modelos ni DTOs. Los ejemplos se revisaron contra los modelos y los
consumidores actuales; no se ejecutaron contra SQL Server ni se añadieron tests.

Los tres helpers actuales consultan una vez por elemento. Con diez autores, diez
dependencias y diez tags hay treinta consultas de búsqueda. Estos reemplazos hacen
una consulta por colección no vacía: tres consultas para ese mismo ejemplo.

Se conservan las firmas, la construcción de relaciones y los elementos null que
Create/Edit comprueban para detectar IDs inexistentes. Se deduplican los IDs antes
de consultar. No se hace SaveChanges dentro de los helpers.

## 1. GetDependencies

Ubicación revisada: `SNEStorage/Services/ResourceService.cs`, método GetDependencies,
consulta dentro del foreach en la línea 926.

Solo necesita saber qué IDs existen; no necesita cargar entidades Resource completas.

```csharp
private async Task<List<ResourceDependency?>> GetDependencies(
    long[] dependencyIds, CancellationToken ct)
{
    var ids = dependencyIds.Distinct().ToArray();
    if (ids.Length == 0)
        return [];

    var existingIds = await _context.Resources
        .AsNoTracking()
        .Where(resource => ids.Contains(resource.Id))
        .Select(resource => resource.Id)
        .ToListAsync(ct);

    var existing = existingIds.ToHashSet();
    List<ResourceDependency?> dependencies = [];

    foreach (var id in ids)
    {
        dependencies.Add(existing.Contains(id)
            ? new ResourceDependency
            {
                Id = 0,
                ResourceId = 0,
                DependencyResourceId = id
            }
            : null);
    }

    return dependencies;
}
```

El foreach trabaja exclusivamente en memoria. Un ID inexistente sigue generando
null. Esto comprueba existencia; las reglas sobre permisos, dependencias privadas,
dependencia del propio recurso y ciclos se validan aparte.

## 2. GetAuthors

Ubicación revisada: `SNEStorage/Services/ResourceService.cs`, método GetAuthors,
consulta dentro del foreach en la línea 958.

Se mantiene la navegación User que construía el helper original. AsTracking conserva
los usuarios existentes como entidades conocidas por el contexto al guardar el grafo
del recurso. No se cargan UserInfo, avatares ni otras colecciones.

```csharp
private async Task<List<ResourceAuthors?>> GetAuthors(
    string[] authorIDs, bool iAmAuthor, string userId, CancellationToken ct)
{
    var ids = authorIDs
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

    if (iAmAuthor && !ids.Contains(userId, StringComparer.OrdinalIgnoreCase))
        ids.Add(userId);

    if (ids.Count == 0)
        return [];

    var users = await _context.Users
        .AsTracking()
        .Where(user => ids.Contains(user.Id))
        .ToListAsync(ct);

    var usersById = users.ToDictionary(user => user.Id,
        StringComparer.OrdinalIgnoreCase);
    List<ResourceAuthors?> authors = [];

    foreach (var id in ids)
    {
        authors.Add(usersById.TryGetValue(id, out var user)
            ? new ResourceAuthors
            {
                ResourceId = 0,
                UserId = user.Id,
                User = user
            }
            : null);
    }

    return authors;
}
```

IAmAuthor añade al usuario actual una sola vez al conjunto de IDs. Esta propuesta
también comprueba su existencia cuando se añade por el checkbox; antes esa rama
construía la relación sin buscarlo. UserId usa el valor canónico del usuario encontrado.
Un autor inexistente sigue generando null para la validación del servicio.

## 3. GetTags

Ubicación revisada: `SNEStorage/Services/ResourceService.cs`, método GetTags,
consulta dentro del foreach en las líneas 996–997.

Se buscan de una vez todos los tags existentes. Después se reutilizan sus entidades
con seguimiento o se construyen tags nuevos que se guardarán junto con el recurso.
Se mantiene ToLower/Trim para la búsqueda; optimizar esa expresión es un trabajo
distinto de eliminar las consultas por elemento.

```csharp
private async Task<List<ResourceTags>> GetTags(
    string[] tagNames, CancellationToken ct)
{
    var names = tagNames
        .Where(name => !string.IsNullOrWhiteSpace(name))
        .Select(name => name.ToLower().Trim())
        .Distinct()
        .ToArray();

    if (names.Length == 0)
        return [];

    var existingTags = await _context.Tags
        .AsTracking()
        .Where(tag => names.Contains(tag.Name.ToLower().Trim()))
        .OrderBy(tag => tag.Id)
        .ToListAsync(ct);

    var tagsByName = existingTags
        .GroupBy(tag => tag.Name.ToLower().Trim())
        .ToDictionary(group => group.Key, group => group.First());

    List<ResourceTags> tags = [];

    foreach (var name in names)
    {
        if (!tagsByName.TryGetValue(name, out var tag))
        {
            tag = new Tag
            {
                Id = 0,
                Name = char.ToUpperInvariant(name[0]) + name[1..]
            };
        }

        tags.Add(new ResourceTags
        {
            TagId = tag.Id,
            Tag = tag
        });
    }

    return tags;
}
```

GroupBy evita que la construcción del diccionario falle si ya existen varios tags
con el mismo nombre normalizado. Se elige el de menor Id; esto no limpia duplicados
que ya estén en la base de datos.

**Comparación de nombres:** la asociación en memoria compara los nombres después
de ToLower/Trim y conserva los acentos como caracteres distintos. No se consultó la
collation de SQL Server. Si la DB considera equivalentes nombres que esa comparación
en C# distingue (por ejemplo cafe/café bajo una collation que ignora acentos), hay que
unificar esa política antes de aplicar este helper: el resultado SQL y el diccionario
podrían discrepar y generar otro tag. Una columna de nombre normalizado con una
collation definida permitiría una comparación e indexación coherentes.

La creación concurrente del mismo tag tampoco se resuelve únicamente con consultas
por conjunto: requiere una clave normalizada única y tratamiento del conflicto si
quieres garantizar unicidad entre solicitudes simultáneas.

## Integración

- Sustituye los tres métodos originales por sus correspondientes propuestas.
- Mantén las llamadas y comprobaciones de null actuales en Create/Edit.
- Conserva el wrapper DBHelperService y su rollback existente.
- Envía el cancellation token del wrapper a cada helper:

```csharp
async cancel => await GetAuthors(dto.AuthorsIDs, dto.IAmAuthor, userId, cancel)
async cancel => await GetDependencies(dto.DependencyIDs, cancel)
```

En Edit la expresión IAmAuthor sigue siendo `dto.IAmAuthor == true`.

Los helpers comparten el DbContext y deben ejecutarse secuencialmente. No utilices
Task.WhenAll para paralelizar estas tres consultas del mismo contexto. EF Core no
admite operaciones paralelas sobre una misma instancia de DbContext.
[Documentación oficial](https://learn.microsoft.com/en-us/ef/core/dbcontext-configuration/#avoiding-dbcontext-threading-issues).

## Otros lugares revisados

### No encontré más bucles con una consulta SQL por elemento

En los archivos actuales de Services y Controllers, las tres búsquedas anteriores
son los casos explícitos encontrados. No encontré configuración de lazy loading
mediante UseLazyLoadingProxies o ILazyLoader. Esta fue una revisión del código, sin
medición del SQL ni del número real de comandos ejecutados.

| Lugar | Observación |
| --- | --- |
| ResourceService.GetTypes | Lee la caché una vez y recorre sus valores en memoria. |
| ResourceService.GetPreviews | Procesa cada archivo; PrepareFile usa la caché de MIME y prepara su entidad. No hay una consulta SQL por preview en ese bucle. |
| FileService.PrepareDeletion | Ya obtiene los archivos mediante un único Where/Contains. |
| ResourceService.SelectFind | La deduplicación posterior de categorías ocurre en memoria. |
| ResourceService.ValidateVersion | La comparación del historial ocurre en memoria después de materializar la consulta. |
| UserNameDTO.FillUserSummaries | Recorre datos cargados; no hace llamadas a la DB dentro del bucle. |
| Comentarios del frontend | El voto inicial se obtiene de Score.LikesUsers. El GET userlike se utiliza para verificar una escritura cuya respuesta se perdió; no se solicita uno por comentario al cargar la lista. |

### Consultas costosas distintas del problema anterior

1. **CommentService.GetStart**, líneas 254–261: carga todos los votos de cada
   comentario y todos sus enlaces de respuestas. Una proyección podría devolver
   likes, dislikes, voto del usuario actual y HasReplies, además del texto/autor/avatar.
   Eso exige acordar un DTO y adaptar el frontend; no se propone cambiar el contrato
   de comentarios como parte de estos tres reemplazos.
2. **CommentService.CheckResourcePermission**, líneas 214–237: carga versiones y
   autores completos para comprobar permisos. Conviene proyectar el permiso con Any,
   como en ResourceService.GetAccess.
3. **ResourceService.AddComment**, líneas 142–146: también carga autores y versiones
   para comprobar acceso. Puede reutilizar el acceso proyectado.
4. **ExternalAuthorService.GetAuthorLike**, rama de búsqueda vacía/corta, líneas
   29–36: falta Take(maxAmount) antes de ToListAsync; actualmente trae todos los nombres.

Estos son problemas de volumen de datos o consultas adicionales por operación;
no son otros foreach con una consulta por elemento. Los agregados Count/Average
dentro de un IQueryable tampoco implican un viaje de red por cada fila.

Una consulta pequeña por conjunto y una proyección de las columnas necesarias son
las medidas principales de esta propuesta. No se cuantificó una mejora de tiempo.
[Recomendaciones oficiales sobre consultas eficientes](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying).
