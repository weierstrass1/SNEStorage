# ResourceController: acciones para las nuevas consultas de ResourceService

Fecha: **2026-10-08**.

Revisé los cambios de `ResourceService`, los DTOs nuevos y el controlador actual. Este documento contiene **las siete acciones nuevas listas para copiar**, un helper de respuesta y los problemas encontrados en la transición al GET reducido.

**No se modificó ningún archivo de backend.** El código de este documento no se compiló ni se ejecutó. Las firmas y los nombres del bloque principal corresponden a los métodos que existen actualmente en tu servicio; los ajustes pendientes están separados en la revisión.

## 1. Contratos propuestos

Conservar `[Route("api/resource")]` en `ResourceController`.

| Acción HTTP nueva | Método del servicio existente | Respuesta de éxito |
|---|---|---|
| `GET /api/resource/{resourceId}/dependencies` | `GetResourceAllDependencies` | `List<ResourceDependencyViewDTO>` |
| `GET /api/resource/{resourceId}/external-dependencies` | `GetResourceAllExternalDependencies` | `List<ResourceExternalDependency>` |
| `GET /api/resource/{resourceId}/authors` | `GetResourceAllAuthors` | `List<UserNameDTO>` |
| `GET /api/resource/{resourceId}/external-authors` | `GetResourceAllExternalAuthors` | `List<ResourceExternalAuthor>` |
| `GET /api/resource/{resourceId}/versions` | `GetResourceAllVersion` | `List<ResourceVersion>` |
| `GET /api/resource/{resourceId}/versions/latest` | `GetResourceLastVersion` | `ResourceVersion` |
| `GET /api/resource/{resourceId}/rating` | `GetRating` | `RatingReadDto` |

Los nombres de ruta de esta tabla son **propuestos**, porque esas acciones todavía no están en el controlador. No llevan `/` inicial en los atributos: así conservan el prefijo `api/resource`.

El nombre `GetResourceAllVersion` del servicio está en singular en tu código; lo uso exactamente así, sin exigir que lo renombres.

## 2. Bloque listo para pegar dentro de ResourceController

Pegar dentro de la clase, antes de su última llave. No reemplaza sus acciones actuales de creación, edición, comentarios, puntuación, archivos, descarga ni options.

Tu archivo ya tiene los `using` necesarios:

```csharp
using Microsoft.AspNetCore.Mvc;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;
```

El helper siguiente desempaqueta el `OneOf` y reutiliza tu `GetActionResult<T>(ResourceServiceResult)` actual. El cuerpo HTTP de éxito contiene los datos y el de error contiene el enum, igual que tus acciones existentes.

```csharp
private ActionResult<OneOf<T, ResourceServiceResult>> GetQueryActionResult<T>(
    OneOf<T, ResourceServiceResult> result)
{
    if (result.TryPickT0(out var data, out var error))
        return Ok(data);

    return GetActionResult<T>(error);
}

[HttpGet("{resourceId:long}/dependencies")]
public async Task<ActionResult<OneOf<List<ResourceDependencyViewDTO>, ResourceServiceResult>>>
    GetDependencies([FromRoute] long resourceId, CancellationToken ct = default)
{
    var result = await _resourceService.GetResourceAllDependencies(resourceId, ct);
    return GetQueryActionResult(result);
}

[HttpGet("{resourceId:long}/external-dependencies")]
public async Task<ActionResult<OneOf<List<ResourceExternalDependency>, ResourceServiceResult>>>
    GetExternalDependencies([FromRoute] long resourceId, CancellationToken ct = default)
{
    var result = await _resourceService.GetResourceAllExternalDependencies(resourceId, ct);
    return GetQueryActionResult(result);
}

[HttpGet("{resourceId:long}/authors")]
public async Task<ActionResult<OneOf<List<UserNameDTO>, ResourceServiceResult>>>
    GetAuthors([FromRoute] long resourceId, CancellationToken ct = default)
{
    var result = await _resourceService.GetResourceAllAuthors(resourceId, ct);
    return GetQueryActionResult(result);
}

[HttpGet("{resourceId:long}/external-authors")]
public async Task<ActionResult<OneOf<List<ResourceExternalAuthor>, ResourceServiceResult>>>
    GetExternalAuthors([FromRoute] long resourceId, CancellationToken ct = default)
{
    var result = await _resourceService.GetResourceAllExternalAuthors(resourceId, ct);
    return GetQueryActionResult(result);
}

[HttpGet("{resourceId:long}/versions")]
public async Task<ActionResult<OneOf<List<ResourceVersion>, ResourceServiceResult>>>
    GetVersions([FromRoute] long resourceId, CancellationToken ct = default)
{
    var result = await _resourceService.GetResourceAllVersion(resourceId, ct);
    return GetQueryActionResult(result);
}

[HttpGet("{resourceId:long}/versions/latest")]
public async Task<ActionResult<OneOf<ResourceVersion, ResourceServiceResult>>>
    GetLatestVersion([FromRoute] long resourceId, CancellationToken ct = default)
{
    var result = await _resourceService.GetResourceLastVersion(resourceId, ct);
    return GetQueryActionResult(result);
}

[HttpGet("{resourceId:long}/rating")]
public async Task<ActionResult<OneOf<RatingReadDto, ResourceServiceResult>>>
    GetRating([FromRoute] long resourceId, CancellationToken ct = default)
{
    var result = await _resourceService.GetRating(resourceId, ct);
    return GetQueryActionResult(result);
}
```

### Acceso y errores de estas acciones

- No llevan `[Authorize]`, porque sus datos también deben poder mostrarse para recursos públicos/no listados a visitantes. **Cada método nuevo del servicio ya comienza con `GetAccess` + `CheckViewAccess`**; esa comprobación controla recursos privados.
- Las acciones no consultan relaciones ni calculan permisos: llaman al servicio y convierten su resultado en respuesta HTTP.
- `CancellationToken ct` se transmite al servicio; no genero otro token ni ejecuto varias queries concurrentes sobre el mismo contexto desde el controlador.
- Una lista vacía válida devuelve **200 con `[]`**. Tu `ItemOrError` comprueba null, no `Count`, por lo que este caso funciona.
- Se conserva tu mapeo actual: errores de inexistencia a 404; `Unauthorized`/`UserNotFound` a 401; los demás a 400. Esto incluye `DBError`, que tu helper actual todavía mapea a 400. Si decides usar 500 para DBError o distinguir 401/403, cambia el helper de errores como decisión separada.
- La lectura de la última versión permite visitante si el recurso es visible. La escritura de versiones continúa exigiendo login y permiso de edición.

## 3. Corrección de firma en una acción existente

La acción actual de archivo devuelve un archivo, pero declara `OneOf<Resource, ResourceServiceResult>` y usa `GetActionResult<Resource>`.

Referencia: [ResourceController.cs:119](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Controllers/ResourceController.cs:119).

Si mantienes el retorno actual de `ResourceService.GetFile` como `Models.File`, reemplaza **esa acción existente**, sin añadir una segunda acción con la misma ruta:

```csharp
[HttpGet("{resourceId:long}/file/{resourceFileId:long}")]
public async Task<ActionResult<OneOf<SNEStorage.Models.File, ResourceServiceResult>>>
    GetFile([FromRoute] long resourceId, [FromRoute] long resourceFileId,
        CancellationToken ct = default)
{
    var result = await _resourceService.GetFile(resourceId, resourceFileId, ct: ct);
    return GetQueryActionResult(result);
}
```

No cambio el endpoint de descarga: sigue devolviendo `PhysicalFile` y mantiene su soporte de rangos. Tampoco cambio la respuesta de creación o de puntuación en el bloque de nuevas lecturas.

## 4. Revisión de los cambios de ResourceService

### Lo que ya encaja con la estructura nueva

- Separaste puntuación, autores, dependencias e historial de la consulta principal.
- `GetAccess` calcula pertenencia con `Any` dentro de la proyección SQL; no necesita materializar todas las versiones/autores.
- Las siete consultas nuevas comprueban acceso antes de devolver resultados.
- `GetRating` calcula promedio/contador/voto propio en la consulta, sin cargar todos los votos.
- `GetResourceLastVersion` ordena por `CreatedAt` descendente y desempata por `Id`, tomando una sola versión.
- El DTO de dependencias conserva ambos IDs: asociación y recurso requerido; no añade Seal of Quality, siguiendo tu última decisión.

### 4.1. Get todavía usa Submitter después de quitar su Include — crítico

Referencias: [ResourceService.cs:434](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:434), [464](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:464).

Ahora la consulta solo incluye videojuego, visibilidad, tipos/categorías y tags. Al final todavía ejecuta:

```csharp
resource.UserSummary = UserNameDTO.FromApplicationUser(resource.Submitter!);
```

`!` suprime la advertencia de nulabilidad, **no carga la navegación ni evita un null**. En una lectura sin tracking y sin Include, `Submitter` no queda cargado por esa consulta; este acceso puede lanzar `NullReferenceException`.

**Qué cambiar tú:** si el Get reducido ya no debe devolver ese resumen, retirar esta construcción. Si quieres el resumen del submitter, obtenerlo mediante una proyección/consulta explícita. No depender de que otra operación haya cargado la entidad.

También siguen los dos `FillUserSummaries` de autores/versiones, en líneas 458–463. Ya no corresponden al Get reducido; normalmente iterarán colecciones vacías. La construcción del resumen debe vivir en la consulta que realmente devuelve esos datos.

### 4.2. Get sigue comprobando privacidad con colecciones que ya no carga — crítico

Referencias: [ResourceService.cs:453](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:453), [684](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:684).

`Get` todavía usa `CheckResourceViability(res)`. Ese helper consulta en C# `resource.ResourceVersions.Any(...)` y `resource.ResourcesAuthors.Any(...)`, sobre la entidad ya materializada.

Con las colecciones sin cargar, puede rechazar a un autor/submitter de versión que debería ver un recurso privado. El submitter principal y el staff pueden pasar por las otras condiciones, pero eso no cubre todos los permisos.

**Qué cambiar tú:** validar con los helpers nuevos antes de leer el recurso básico:

```csharp
var access = CheckViewAccess(await GetAccess(resourceId, ct));
if (access != ResourceServiceResult.Success)
    return access;
```

Después realizar la consulta básica, desempaquetar `res` y comprobar null; no volver a comprobar visibilidad con colecciones sin cargar. Si necesitas garantías ante un cambio concurrente de permisos, repetir el filtro de visibilidad en la consulta de datos o definir la transacción correspondiente.

No aplicar este reemplazo mecánicamente a todos los callers de `CheckResourceViability`: `GetFile` actualmente sí carga autores y versiones. El problema aquí es que cambiaste las relaciones cargadas por `Get`.

### 4.3. Edit continúa dependiendo del Get antiguo — crítico

Referencias: [ResourceService.cs:187](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:187), [199](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:199), [870](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:870).

Todavía hace:

```csharp
var res = await Get(resourceId, true, ct);
var check = CheckResourceIsEditable(res!);
var mainFolder = GetMainResourceFolder(resource);
```

Al reducir Get, aparecen tres problemas:

1. El permiso de edición usa autores/submitters de versiones sin cargar y puede denegarse incorrectamente.
2. `GetMainResourceFolder` hace `ResourceVersions.First().File...`; sin versiones cargadas puede lanzar una excepción.
3. La lógica que conserva/elimina relaciones parte de colecciones no cargadas. Puede dejar de conservarlas correctamente, no aplicar eliminaciones o añadir relaciones duplicadas; no debes asumir que una colección inicializada vacía representa las filas existentes.

**Qué cambiar tú:** separar la carga de escritura, con tracking, y utilizar `GetAccess`/`CheckEditAccess` para permisos. Esa carga debe traer las asociaciones que Edit modifica: tipos, tags, previews, autores, autores externos, dependencias y dependencias externas. La carpeta base se obtiene con una consulta de una URL, sin traer todo el historial para ello.

Esto es el **punto 11** del documento anterior, adaptado a tu estructura. El punto 12 explica cómo consultar la carpeta base para añadir versiones. Las lecturas HTTP nuevas no sustituyen la carga con tracking del servicio de edición.

### 4.4. Previews quedaron fuera de Get y aún no hay método de lectura público — necesario para la vista

Referencia: [ResourceService.cs:434](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:434).

También quitaste `ResourcePreviews` de Get. Entre los métodos nuevos no hay una lectura pública que devuelva previews; el `GetPreviews` existente es privado y **prepara uploads**, no lee previews de la DB.

**Qué falta implementar tú:** conservar la lectura de previews en Get, o crear un método separado que devuelva como mínimo `Id` de la asociación y `FileId`, más los metadatos de tipo necesarios para el carrusel. El `Id` de asociación sirve para eliminar previews al editar.

El bloque de controlador de este documento no inventa una llamada a un método inexistente. Cuando lo tengas, podemos añadir la acción, por ejemplo `GET /api/resource/{resourceId}/previews`, y adaptar el carrusel.

### 4.5. FileTypeId del avatar está tomando File.Id — corrección puntual

Referencia: [ResourceService.cs:957](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:957).

En `GetResourceAllAuthors` tienes:

```csharp
FileTypeId = r.User.UserInfo.Avatar.Id,
```

Debe ser:

```csharp
FileTypeId = r.User.UserInfo.Avatar.FileTypeId,
```

Si permites usuario sin UserInfo/avatar, proyectar `Avatar = null` en ese caso, en lugar de construir obligatoriamente un `FileResponse` con campos no disponibles. La proyección de UsernameColor también puede contemplar ausencia de UserInfo. El avatar está ahora explícitamente en tu consulta separada; este documento respeta ese retorno y señala el ID equivocado.

### 4.6. La lista de autores perdió el ID de asociación requerido por el editor

Referencias: [ResourceService.cs:939](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:939), [ResourceEditDTO.cs:21](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/DTOs/ResourceEditDTO.cs:21).

`GetResourceAllAuthors` devuelve `UserNameDTO.Id = r.UserId`, que identifica al usuario. Eso alcanza para nombre/color/enlace de perfil en la vista individual.

Para edición, `RemoveAuthorsIDs` es `long[]` y el servicio compara contra `ResourceAuthors.Id`: **es el ID de la asociación, no el ID del usuario**. Con esta respuesta sola no puedo rellenar correctamente esa lista de eliminaciones.

**Opciones para que decidas tú:** devolver un DTO que incluya el ID de asociación y el resumen de usuario, o tener una lectura específica para el editor. Un contrato mínimo posible sería:

```csharp
public sealed record ResourceAuthorViewDTO(long Id, UserNameDTO UserSummary);
```

Este DTO es una propuesta para ese ajuste, **no está en el servicio actual ni en las acciones copiables del punto 2**. Si cambias el retorno del servicio, cambia también `GetAuthors` a ese mismo tipo; el helper genérico de respuesta se reutiliza.

### 4.7. Dependencias: el permiso del recurso padre no autoriza mostrar el nombre de cualquier dependencia privada

Referencia: [ResourceService.cs:898](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:898).

La consulta comprueba acceso al recurso que estás viendo y después proyecta directamente `r.DependencyResource.Name`. Si ese recurso tiene una dependencia privada que el visitante no puede ver, hoy se devuelve su nombre.

**Qué cambiar tú, según la política de privacidad que acordamos:** calcular acceso a cada recurso dependido en la proyección y usar `Name = null` cuando no sea visible, o filtrar esas dependencias. El DTO ya acepta `string? Name`. La opción de nombre null conserva el vínculo y permite mostrar `Private resource` en frontend. No hace falta cargar usuarios/versiones de la dependencia: las condiciones se pueden expresar con `Any` en SQL, como en `GetAccess`.

No añado Seal of Quality a la dependencia ni queries extra para ese sello.

### 4.8. Última versión inexistente usa ResourceNotFound en lugar de VersionNotFound

Referencia: [ResourceService.cs:999](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:999).

`ItemOrError(res)` usa `ResourceNotFound` por defecto. En este método ya comprobaste que el recurso existe: si no tiene versiones, conviene distinguirlo.

**Cambio puntual propuesto en ese return:**

```csharp
return ItemOrError(res, ResourceServiceResult.VersionNotFound)!;
```

El controlador ya mapea ambos errores a 404. El frontend puede mostrar `No releases are available` en esa sección ante `VersionNotFound`, mientras sigue mostrando el resto del recurso; un `ResourceNotFound` real corresponde al recurso inexistente.

`Take(1).FirstOrDefaultAsync` es redundante, pero no es el problema: el orden y el desempate de esta consulta están bien. Puedes dejar solo `FirstOrDefaultAsync` después del ordenamiento. Su Include de File entrega metadatos; no carga los bytes del archivo.

### 4.9. Sigue pendiente usar FileId al preparar la eliminación de previews

Referencia: [ResourceService.cs:331](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:331).

Aunque este bloque no cambió en el diff nuevo, continúa este problema en Edit:

```csharp
.PrepareDeletion(removePrs.Select(pr => pr.Id), cancel)
```

`PrepareDeletion` busca `File.Id`; pasar `ResourcePreview.Id` puede seleccionar un archivo diferente por coincidencia numérica o no seleccionar el archivo correcto. Usar `pr.FileId` y limitar la consulta de previews eliminadas al `ResourceId` que se está editando.

Es independiente del controlador: añadir las nuevas acciones no arregla este bloque.

### 4.10. Ordenar dependencias antes de construir el record

Referencia: [ResourceService.cs:898](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:898).

En `GetResourceAllDependencies` proyectas con el constructor posicional de `ResourceDependencyViewDTO` y después ordenas por propiedades del DTO. EF Core tiene una limitación reportada al componer `OrderBy` sobre este tipo de proyección por constructor. Es un **riesgo de traducción identificado por revisión**, no una excepción reproducida contra tu EF Core 10.0.12. [Issue oficial de EF Core](https://github.com/dotnet/efcore/issues/34562).

Para evitar depender de esa traducción, mover el orden antes del Select y usar las columnas/navegaciones de la entidad:

```csharp
var res = await _dbHelperService.TryFunction(
    cancel => _context.ResourceDependencies.AsNoTracking()
        .Where(r => r.ResourceId == resourceId)
        .OrderByDescending(r => r.DependencyResource!.Name)
        .ThenByDescending(r => r.Id)
        .Select(r => new ResourceDependencyViewDTO(
            r.Id, r.DependencyResourceId, r.DependencyResource!.Name))
        .ToListAsync(cancel),
    ResourceServiceResult.DBError, ct: ct);
```

Este bloque muestra el cambio de orden/proyección; integrar también la política de nombre privado del punto 4.7. Conservar el control de acceso y el return existentes del método. La proyección de autores actual usa un inicializador de propiedades de UserNameDTO, por lo que no es el mismo caso que este record posicional.

## 5. Datos/acciones todavía necesarios para adaptar toda la vista

| Necesidad de frontend | Estado actual |
|---|---|
| Recurso básico: nombre/descripción/tipos/categorías/tags | Get existe, pero necesita corregir las referencias a navegaciones retiradas y su permiso. |
| Rating | Método nuevo existente; acción incluida en este documento. |
| Autores y autores externos | Métodos nuevos existentes; acciones incluidas. Para eliminación de autores en editor falta el ID de asociación. |
| Dependencias y dependencias externas | Métodos nuevos existentes; acciones incluidas. Revisar privacidad del nombre de dependencias. |
| Última versión | Método nuevo existente; acción incluida. |
| Historial de versiones | Método nuevo existente; acción incluida. Actualmente devuelve todo el historial, sin paginación. |
| Previews | Falta lectura explícita o reincorporarlas a Get. |
| Permiso CanEdit de la vista | `GetAccess` y `CheckEditAccess` existen, pero son privados y no hay acción/propiedad pública que comunique ese permiso. |
| TotalDownloads | No hay agregado expuesto para el total de todas las versiones. El Downloads de la última versión es solo el de esa versión. |

Para CanEdit, puedes añadir el booleano al resultado del Get reducido o un método público de acceso para una acción separada. El frontend no debe bajar todo el historial solo para descubrir si el usuario subió alguna versión y por eso puede editar. El PUT debe seguir verificando permisos por su cuenta.

Para TotalDownloads, calcular un `Sum` en SQL sobre todas las versiones. No obtenerlo sumando una página del historial ni confundirlo con el contador de la última versión.

No propongo acciones que llamen a métodos privados o que todavía no existen. El código copiable del punto 2 usa exclusivamente los siete métodos públicos que encontré.

## 6. Cómo se conectaría el frontend después

Los cambios de este documento todavía no se aplicaron a las vistas.

- Get básico alimenta nombre, descripción, metadata y flags.
- Rating, autores, dependencias y última versión tienen estados de carga/error independientes.
- El historial se solicita al abrir su tab, sin bloquear la descarga de la última versión.
- Una lista cargada vacía es distinta de una lista que todavía está cargando.
- Los textos visibles permanecen en inglés: `Loading authors…`, `Loading dependencies…`, `Loading latest version…`.
- El sello/dorado del título depende de `SealOfQuality` de la última versión y se actualiza cuando llegue esa respuesta.
- Errores de una sección no deben sustituir todo el recurso por una pantalla genérica si el recurso básico se cargó correctamente; errores reales de acceso/inexistencia se manejan según su alcance.
- Si se cambia de recurso/sesión, las respuestas viejas no deben escribirse sobre la nueva vista.

## 7. Orden recomendado para aplicar

1. Corregir Get (resúmenes y permisos) y separar la carga con tracking usada por Edit.
2. Corregir FileTypeId del avatar y FileId para eliminación de previews.
3. Decidir lectura de previews y exposición de CanEdit; conservar IDs de asociación para el editor.
4. Pegar el helper y las siete acciones del punto 2; corregir la firma de GetFile si quieres incluir ese ajuste.
5. Corregir el error de última versión inexistente, el nombre de dependencias privadas y ordenar dependencias antes de construir el DTO.
6. Adaptar el frontend a las rutas finales y a los nuevos estados de carga, cuando me confirmes los contratos.

La incorporación de estas acciones no garantiza por sí sola una mejora medida de rendimiento. No se ejecutaron tests, build ni consultas a SQL Server en esta revisión.
