# Revisión de flags y likes de noticias

Fecha: 15 de octubre de 2026. Revisión de código y compilación; no se aplicaron migraciones ni se ejecutaron pruebas contra una base de datos.

Se revisaron FlagController, FlagsService y sus definiciones, DTOs, relaciones, NewsController, NewsService y las migraciones FlagsAndNewsLikes/FlagsAndNewsLikes2. Los cambios concurrentes del servicio de recursos quedan fuera de esta revisión.

## Migraciones: estado de esta revisión

El propietario confirmó que FlagsAndNewsLikes no se había aplicado. En esa migración se corrigió:

1. `new.ScoreId` se crea temporalmente nullable, sin asignar 0 a todas las noticias.
2. Un bloque SQL genera un Score distinto por noticia existente y guarda la correspondencia usando OUTPUT de MERGE. No se supone que los IDs de noticias y Scores coincidan ni que sean consecutivos.
3. Se rellena ScoreId, se convierte a NOT NULL y después se añade la FK.
4. `reason.priority` comienza en 1, dentro del rango 1–5 del modelo. Es una prioridad inicial, no una clasificación automática de los motivos existentes.
5. La FK nueva de comment_flags se llama FK_comment_flags_flag. Se alinearon Designer y snapshot; el propietario corrigió el Build correspondiente.

Posteriormente el propietario creó FlagsAndNewsLikes2. **Su Up y Down están vacíos**: no añaden ninguna transformación de datos. La asignación de Scores continúa en FlagsAndNewsLikes.

La subida conserva el contenido y los IDs de las noticias existentes y añade sus Scores. La reversión Down elimina ScoreId pero no limpia esos Scores: quedarían huérfanos si se revierte. No se modificó Down después de que el propietario indicó haber resuelto las migraciones.

Referencias: [personalizar migraciones y rellenar columnas antes de hacerlas obligatorias](https://learn.microsoft.com/en-us/ef/core/managing-schemas/migrations/managing#data-operations), [MERGE y OUTPUT en SQL Server](https://learn.microsoft.com/en-us/sql/t-sql/statements/merge-transact-sql?view=sql-server-ver17).

## Pendientes, por prioridad

### 1. P1 — La lista de reportes es pública

**Dónde:** `Controllers/FlagController.cs:18`, `Services/FlagsService.cs:52`.

FullGet tiene AllowAnonymous y el servicio no comprueba permisos. Cualquier visitante puede consultar quién reportó, el motivo, prioridad y estado. El índice y el captcha de creación no restringen esta lectura.

**Corregir:** retirar AllowAnonymous, exigir autenticación y verificar IsModeratorOrAdmin antes de consultar la lista. Usa la misma comprobación que ValidateEdit: Unauthorized sin sesión, Forbidden para usuarios sin permisos. Exigir solo login todavía permitiría que cualquier usuario registrado viera los reportes.

### 2. P1 — Se busca el elemento usando ReasonID

**Dónde:** `Services/Definitions/FlagService/CreateResourceDefinition.cs:45` y `CreateCommentDefinition.cs:45`.

La búsqueda del motivo usa correctamente ReasonID. La segunda consulta también usa ReasonID, pero debe buscar el objeto reportado:

```csharp
GetByPropertyQuery<Resource, long> getResourceByProperty = new(r => r.Id,
    create.ResourceID, ServiceResult.ResourceNotFound);
```

```csharp
GetByPropertyQuery<Comment, long> getCommentByProperty = new(r => r.Id,
    create.CommentID, ServiceResult.CommentNotFound);
```

Ahora puede rechazarse un recurso/comentario existente porque no coincide con el motivo, o superar la validación con un objeto diferente y fallar al guardar por FK.

### 3. P1 — Se puede reportar un recurso privado sin acceso

**Dónde:** ambos ValidateCreate de FlagsService.

Se comprueba autenticación y existencia, pero no permiso para ver el objetivo. Tras corregir los IDs, cualquier usuario autenticado podrá enviar un reporte contra un recurso privado ajeno conociendo su ID, o contra uno de sus comentarios.

**Corregir:** para recursos, reutilizar ResourceAccessService.GetViewAccess. Para comentarios, comprobar acceso al recurso relacionado cuando exista; no imponer que todos los comentarios pertenezcan a un recurso, dado que el modelo permite otros contextos. Si no existe relación a recurso, usar las reglas del contexto correspondiente.

Además, el flujo no comprueba Resource.AlreadyModerated. Si continúa vigente la regla de deshabilitar reportes para recursos ya moderados, aplícala también en el servicio. Ocultar el botón no impide invocar directamente la API.

### 4. P2 — Los filtros no se enlazan desde la query

**Dónde:** `DTOs/FlagQueryDTOs.cs:18`.

FlagFilterDTO declara campos públicos. El model binder de este DTO debe recibir propiedades públicas con setters; por eso la petición puede devolver una lista sin aplicar los filtros enviados.

```csharp
public class FlagFilterDTO
{
    [Range(1, 5)]
    public int? LowestPriority { get; set; }
    [Range(1, 5)]
    public int? HighestPriority { get; set; }
    public bool? Resolved { get; set; }
    public long? ReasonID { get; set; }
    public DateTime? CreatedAtFrom { get; set; }
    public DateTime? CreatedAtTo { get; set; }
}
```

Valida también LowestPriority <= HighestPriority y CreatedAtFrom <= CreatedAtTo. `to.Date.AddDays(1)` debe tratar DateTime.MaxValue, ya que desbordaría antes de llegar al helper de consultas.

Referencia: [Model Binding de ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/model-binding?view=aspnetcore-10.0).

### 5. P2 — La respuesta no identifica qué se reportó

**Dónde:** `DTOs/FlagQueryDTOs.cs:36`, `Services/Definitions/FlagService/FullGetDefinition.cs:32`.

FlagReadDTO solo identifica el reporte, su autor y el motivo. No incluye ResourceId ni CommentId. Un panel no puede abrir el recurso/comentario denunciado con esta respuesta.

**Corregir:** añadir IDs nullable del objetivo y proyectarlos desde ResourceFlags/CommentFlags. Para un comentario conviene incluir además el ResourceId de su ResourceComment cuando exista. No devolver todo el grafo de entidades para resolverlo.

### 6. P2 — Resolver no actualiza ResolvedAt

**Dónde:** `Services/Definitions/FlagService/EditSingleDefinition.cs:12`.

Solo se asigna Resolved, así que ResolvedAt permanece null aunque se resuelva el reporte.

Una política sencilla conserva la fecha si se repite la misma petición y la elimina al reabrir:

```csharp
item.Resolved = edit;
item.ResolvedAt = edit ? item.ResolvedAt ?? DateTime.UtcNow : null;
```

### 7. P2 — Los likes de noticias todavía no tienen un contrato de lectura completo

**Dónde:** `DTOs/NewsQueryDTOs.cs:6`, `Services/Definitions/NewsService/FullGetDefinition.cs:11`, NewsController.

POST permite añadir/cambiar/quitar votos, pero NewsReadDTO solo añade ScoreId. No se devuelven cantidades de likes/dislikes ni el voto del visitante. Tampoco hay un GET equivalente al userlike de comentarios. Por tanto, el frontend no puede reconstruir el estado al abrir la noticia.

**Propuesta:** proyectar LikeCount, DislikeCount y bool? CurrentUserLike en el DTO de lectura que corresponda. null significa que el visitante no votó; true es like y false es dislike. Obtenerlos mediante Count y la selección del voto del usuario en SQL, sin Include de todos los votos. Alternativamente, usar una acción separada para ese resumen.

### 8. P2 — La relación de noticias con Score está definida por convenciones

**Dónde:** `Models/Builds/NewBuild.cs:10`; la FK generada está en FlagsAndNewsLikes.

El modelo actual produce una relación de muchas noticias a un Score y Cascade desde Score hacia noticia. Esto permite compartir Score entre noticias y borrar una noticia al eliminar su Score. Borrar una noticia no elimina automáticamente su Score.

**Propuesta:** definir explícitamente la relación, decidir unicidad por ScoreId y usar NoAction si se busca el mismo comportamiento que recursos/comentarios. Si la noticia debe limpiar su Score al borrarse, agregar limpieza explícita o un trigger, siguiendo el patrón existente. Al añadir un trigger, declarar UseSqlOutputClause(false) en la tabla para las operaciones EF pertinentes.

Los triggers existentes de recursos/comentarios no consultan la tabla new. Si se permitiera compartir un Score con una noticia, podrían intentar borrarlo y el Cascade actual alcanzaría esa noticia. Cada noticia debe conservar su propio Score.

### 9. P2 — El chequeo de reporte repetido no es atómico

**Dónde:** AnyAsync de ambos ValidateCreate y posterior CreateInternal.

Dos solicitudes simultáneas pueden superar AnyAsync y guardar dos reportes del mismo usuario contra el mismo objetivo. El índice único en FlagId solo impide reutilizar un mismo Flag entre dos filas de esa tabla; no protege usuario + objetivo.

**Corregir:** si la regla debe cumplirse estrictamente, usar una restricción que pueda expresar usuario + objetivo o una transacción con aislamiento/bloqueo adecuados. Mantener AnyAsync sirve para el mensaje normal, pero no sustituye esa protección.

En noticias ya existe el índice único (ScoreId, UserId): evita dos votos, aunque dos primeras peticiones simultáneas pueden acabar una de ellas en DBError. Tratar ese conflicto de unicidad permitiría devolver un resultado más preciso o reintentar la actualización.

### 10. P2 — Borrar el objetivo puede dejar el Flag sin objetivo

**Dónde:** `Models/Builds/ResourceFlagsBuild.cs:22` y `CommentFlagsBuild.cs:22`.

El borrado físico de un recurso/comentario elimina su fila de enlace. No borra hacia arriba el Flag. Puede quedar en la lista de pendientes un reporte cuyo objeto ya no existe.

**Decidir:** si se necesita conservar un historial, guardar información suficiente del objetivo y un estado adecuado. Si no, limpiar también el Flag al borrar físicamente su objetivo. No invertir Cascade indiscriminadamente: hay varias rutas desde usuarios y SQL Server puede rechazarlas.

### 11. P3 — Errores y cancelaciones menores

- `EditSingleDefinition.cs:19`: FindEdit devuelve NewNotFound cuando debe ser FlagNotFound.
- `DTOs/ServiceResultStatics.cs:393`: RepeatedFlag usa 404. Conflict (409) describe mejor un reporte ya existente.
- Ambos ValidateCreate omiten `ct: ct` en las dos llamadas Execute: sus lecturas no reciben la cancelación de la petición.
- `CommentFlagsBuild.cs:19`: CommentId se almacena como resource_id. Funciona, pero el nombre es confuso. Si se cambia, generar RenameColumn para conservar datos.

### 12. P3 — La escritura de likes carga más datos de los necesarios

**Dónde:** `Services/NewsService.cs:81`.

AddEditOrDeleteLike llama a Get, que proyecta texto completo, título y avatar del autor. Para votar solo necesita comprobar existencia y obtener ScoreId. Proyectar ese valor en una consulta pequeña evitará esos datos y joins, manteniendo el DBHelper y el manejo de NewNotFound.

## Lo que está bien

- Create/Edit de noticias validan permisos de moderador/admin desde el servicio.
- Edit de flags también valida permisos desde el servicio.
- Las escrituras públicas de flags exigen autenticación, captcha y rate limiting.
- Los likes validan el enum y mantienen el cambio de like a dislike sobre la misma fila.
- El índice único existente de LikeUsers protege usuario + Score.
- Crear una noticia con `Score = new()` permite a EF insertar y enlazar ambas entidades en SaveChanges.
- Las lecturas usan proyecciones y paginación sin cargar colecciones completas de votos.

No se modificaron servicios, controladores ni DTOs durante esta revisión. El frontend no se amplió con reportes o likes de noticias: esta petición fue de revisión y todavía faltan los contratos de lectura indicados.
