# Mejoras del backend para la carga de páginas

Revisión: 16 de octubre de 2026. **Propuestas: no se modificaron servicios,
controladores, modelos, Program.cs ni migraciones en esta revisión.**

Las ubicaciones corresponden al código actual. Esta lista identifica costes a
partir de las consultas y los endpoints; todavía no dispone de tiempos SQL ni
planes de ejecución de producción. No son fallos de funcionamiento ni garantías
numéricas de mejora. El diagnóstico de red está en
[rendimiento-produccion-2026-10-16.md](rendimiento-produccion-2026-10-16.md).

## Orden recomendado

| Prioridad | Ubicación | Cambio |
| --- | --- | --- |
| 0 | Program / EF / acciones de lectura | Medir tiempo total del servidor y duración/cantidad de SQL. |
| 1 | ResourceController + servicios de recurso/acceso | Un agregado de datos secundarios con una comprobación de acceso. |
| 1 | Builds de ResourceVersion, New y Resource | Índices compuestos para los órdenes de lectura habituales. |
| 2 | ResourceVersionService | Leer una única versión para su edición. |
| 2 | ResourceController + ResourceService | DTO del editor con los datos editables, sin historial completo. |
| 2 | ResourceService.GetResourceLike | Devolver Id y Name en las sugerencias. |
| 3 | FullGet de recursos | Revisar SQL real, última versión y consultas divididas. |
| 3 | ScoreUsers / LikeUsers | Evaluar cobertura del índice existente para agregados. |
| 4 | AccountService / ProfileController | Respuesta mínima de sesión si el perfil sigue siendo costoso. |

## 0. Medir dónde está la espera

**Antes de atribuir una pausa a SQL:** en las mediciones públicas anteriores,
los GET pequeños variaban y un JSON estático también tardó más de un segundo.
El tiempo del navegador incluye red, Cloudflare, hosting y la aplicación.

En Program.cs se puede medir el tiempo hasta comenzar la respuesta, incluyendo
los middlewares que siguen al medidor:

```csharp
using System.Diagnostics;
using System.Globalization;

// Colocar antes de autenticación/autorización y del mapeo de endpoints.
app.Use(async (context, next) =>
{
    long started = Stopwatch.GetTimestamp();
    context.Response.OnStarting(() =>
    {
        string duration = Stopwatch.GetElapsedTime(started)
            .TotalMilliseconds.ToString("0.##", CultureInfo.InvariantCulture);
        context.Response.Headers.Append("Server-Timing", $"app;dur={duration}");
        return Task.CompletedTask;
    });
    await next(context);
});
```

`Server-Timing: app` mide el tramo de la aplicación hasta escribir cabeceras;
no es solo SQL ni la transferencia completa del cuerpo. Complementarlo con los
logs temporales de `Microsoft.EntityFrameworkCore.Database.Command` a Information:
contar comandos y sumar sus duraciones por petición usando TraceIdentifier/scopes.
No habilitar SensitiveDataLogging en producción. Comparar varias muestras con y
sin sesión, primera petición y peticiones posteriores.

- Servidor rápido y navegador lento: investigar transferencia/red/renderizado.
- Muchos comandos pequeños: reducir comprobaciones y viajes repetidos.
- Uno o pocos comandos caros: revisar plan, índices, lecturas y forma de la query.

## 1. Agregar los datos secundarios del recurso

**Actual:** [ResourceService.cs:114](../SNEStorage/Services/ResourceService.cs:114); [ResourceService.cs:474](../SNEStorage/Services/ResourceService.cs:474); [ResourceService.cs:507](../SNEStorage/Services/ResourceService.cs:507); [ResourceVersionService.cs:161](../SNEStorage/Services/ResourceVersionService.cs:161); [ResourceCommunityService.cs:120](../SNEStorage/Services/ResourceCommunityService.cs:120); [ResourceAccessService.cs:50](../SNEStorage/Services/ResourceAccessService.cs:50); [AccountService.cs:742](../SNEStorage/Services/AccountService.cs:742).

La vista pide el GET principal y después siete acciones secundarias. Cada acción
con cookie válida pasa por ValidateJwt, que consulta SecurityStamp. Las lecturas
secundarias además vuelven a llamar GetViewAccess. La nueva preparación del
frontend adelanta esas peticiones, pero no elimina su trabajo en el servidor.

### Propuesta

Mantener el GET principal reducido y añadir:

```text
GET /api/resource/{resourceId}/overview
```

Respuesta: autores internos, autores externos, dependencias internas/externas,
última versión, rating y metadatos de previews. El historial completo sigue en
su acción independiente y se pide al abrir el tab.

El método del agregado comprueba GetViewAccess **una vez** y luego obtiene esas
proyecciones. Los endpoints independientes conservan sus propios controles para
reintentos y otros consumidores. Si el agregado llama a los siete métodos públicos
actuales, repetirá las comprobaciones y perderá buena parte del beneficio.

Puede utilizar una proyección DTO con colecciones/AsSplitQuery o consultas
secuenciales. Una petición HTTP no exige una sola sentencia SQL. No ejecutar
Task.WhenAll sobre el mismo DbContext. Al implementarlo no se elimina la
validación del [AccountService.cs:742](../SNEStorage/Services/AccountService.cs:742) ni se cachean permisos entre peticiones.

**Beneficio esperado:** siete solicitudes secundarias pasan a una, reduciendo
validaciones de sesión, acceso y transporte. El título y la descripción siguen
pudiendo aparecer antes. El efecto sobre SQL se debe medir.

## 2. Índices para fechas y última versión

**Actual:** [ResourceVersionService.cs:161](../SNEStorage/Services/ResourceVersionService.cs:161); [ResourceVersionService.cs:144](../SNEStorage/Services/ResourceVersionService.cs:144); [FullGetDefinition.cs:43](../SNEStorage/Services/Definitions/NewsService/FullGetDefinition.cs:43); [FullGetDefinition.cs:55](../SNEStorage/Services/Definitions/ResourceService/FullGetDefinition.cs:55).
Los Builds revisados no declaran estos índices compuestos. Eso no demuestra que
la DB no los tenga: pueden existir mediante migraciones antiguas o SQL manual.
Comprobar los índices reales antes de crear otro equivalente.

### ResourceVersion.Build: candidato de prioridad alta

```csharp
entity.HasIndex(version => new
    { version.ResourceId, version.CreatedAt, version.Id })
    .IsDescending(false, true, true);
```

Coincide con `Where(ResourceId == id)`, seguido de
`OrderByDescending(CreatedAt).ThenByDescending(Id)`. Ayuda a localizar la última
versión y a recorrer el historial de un recurso. Esas consultas existen también
en la proyección del finder.

### New.Build: candidato de prioridad alta

```csharp
entity.HasIndex(news => new { news.CreatedAt, news.Id })
    .IsDescending(true, true);
```

Coincide con la lista de noticias más recientes. No incluir `Text` nvarchar(max)
en el índice por defecto: ampliaría mucho el índice.

### Resource.Build: elegir según el plan de las listas

Orden general por más reciente:

```csharp
entity.HasIndex(resource => new { resource.CreatedAt, resource.Id })
    .IsDescending(true, true);
```

Para una lista principalmente anónima/pública, evaluar también como alternativa:

```csharp
entity.HasIndex(resource => new
    { resource.VisibilityId, resource.CreatedAt, resource.Id })
    .IsDescending(false, true, true);
```

No añadir ambos automáticamente. La query autenticada incluye condiciones OR
para autores/submitters/staff; no siempre aprovechará un índice que empiece por
VisibilityId. Verificar el plan y el volumen real. Los FK simples pueden estar
indexados por convención; aquí se proponen compuestos para filtro + orden.

Los cambios de Build necesitan migración y aplicación en la DB publicada.
La correspondencia entre columnas/orden importa en índices compuestos.
[Documentación de índices de EF Core](https://learn.microsoft.com/en-us/ef/core/modeling/indexes).

## 3. Obtener una versión concreta para editar

**Actual:** [ResourceVersionService.cs:144](../SNEStorage/Services/ResourceVersionService.cs:144). El editor solicita todas las versiones y el frontend
selecciona una por Id. Esto crece con cada release y transporta notas, archivos y
versiones que ese formulario no necesita.

Añadir por ejemplo:

```text
GET /api/resource/{resourceId}/versions/{versionId}/edit-data
```

DTO mínimo de edición:

```csharp
public sealed record ResourceVersionEditReadDto(
    long Id,
    long ResourceId,
    string Version,
    string? ChangeLog,
    bool SealOfQualityRequested);
```

En el servicio: verificar acceso de edición y filtrar **ambos IDs** antes de
materializar. No incluir File ni todas las versiones del recurso.

```csharp
var query = _context.ResourceVersions.AsNoTracking()
    .Where(version => version.ResourceId == resourceId && version.Id == versionId)
    .Select(version => new ResourceVersionEditReadDto(
        version.Id, version.ResourceId, version.Version!,
        version.ChangeLog, version.SealOfQualityRequested));
```

Usar tu GetFirstOrErrorQueryOperation del nuevo DTO para mapear null a
VersionNotFound. El frontend puede cambiar a este contrato cuando exista.

## 4. Datos de edición en una acción dedicada

**Actual:** [ResourceService.cs:474](../SNEStorage/Services/ResourceService.cs:474); [ResourceService.cs:507](../SNEStorage/Services/ResourceService.cs:507); [ResourceAccessService.cs:50](../SNEStorage/Services/ResourceAccessService.cs:50). El editor ya pide las cinco colecciones en
paralelo desde el navegador; el backend aún valida acceso separadamente.

Proponer `GET /api/resource/{id}/edit-data` con la validación de edición actual,
seguida del ResourceReadDTO y las cinco colecciones editables. Conservar:

- IDs de las relaciones para eliminar enlaces concretos, como ya hace el editor.
- IDs de usuario y resúmenes de autores internos.
- IDs de dependencias y sus nombres permitidos.
- Previews con metadatos para mantener/eliminar.
- Autores y dependencias externas con sus IDs y campos editables.

No necesita rating, likes, historial completo ni bytes de archivos. El catálogo
público de opciones puede seguir separado: el frontend ahora lo comparte durante
cinco minutos en la pestaña. Mantener la política de edición vigente.

Este cambio se puede combinar con el punto 1 mediante proyecciones compartidas,
pero no obliga a que el DTO público exponga datos de gestión.

## 5. Sugerencias de dependencias: Id y Name

**Actual:** [ResourceService.cs:66](../SNEStorage/Services/ResourceService.cs:66). El selector genérico está configurado como
`GetLikeBySelectorQueryOperation<Resource, Resource>` y proyecta la entidad
completa. El picker solo necesita Id y Name.

Cambios propuestos:

1. Constructor/campo: `GetLikeBySelectorQueryOperation<Resource, IdNamePair>`.
2. Firma de GetResourceLike: `OneOf<List<IdNamePair>, ServiceResultContext>`.
3. Parámetro: `GetLikeBySelectorQuery<Resource, IdNamePair>`.
4. Selector: `resource => new IdNamePair { Id = resource.Id, Name = resource.Name }`.
5. ResourceController.GetResourcesLike: `ActionResult<List<IdNamePair>>`.

Conservar GetFilter para la edad/contenido y las comprobaciones actuales de
visibilidad. Las sugerencias no necesitan Description, flags o repositorio.
Este ajuste reduce datos, sin cambiar el shape Id/Name que consume el picker.

## 6. Revisar la proyección de la lista antes de cambiar AsSplitQuery

**Actual:** [FullGetDefinition.cs:216](../SNEStorage/Services/Definitions/ResourceService/FullGetDefinition.cs:216). La lista pagina antes de proyectar, lo cual ya está bien.
GetSelect contiene varias colecciones y usa AsSplitQuery. Evita el producto de
filas de colecciones, pero implica varios comandos/viajes SQL. No es por sí
mismo un N+1 por cada recurso.
[Explicación de consultas divididas](https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries).

Acciones concretas:

- Inspeccionar el SQL generado y su plan para la lista pública y autenticada.
- La última versión se obtiene para MainVersion, Version y VersionCreatedAt con
  tres expresiones ordenadas. Proyectar una única fila Latest en una etapa intermedia
  y reutilizar sus campos si el SQL resultante comparte ese acceso; comprobar que
  EF no vuelva a expandirlo en subconsultas equivalentes.
- Mantener el desempate por Id y la paginación previa a colecciones.
- Si el SQL del finder repite trabajo caro en cada split, evaluar obtener la página
  de IDs/datos principales y completar las etiquetas con consultas por lote para
  esos IDs. Son consultas por lote, no una consulta por elemento.
- No cambiar a AsSingleQuery sin comparar el número de filas/lecturas: una sola
  query puede ser más lenta por multiplicación de filas.

Esta optimización requiere medir; con pocos recursos puede ser menor que la
latencia de red o la repetición de autenticación/acceso.

## 7. Agregados de votos: aprovechar los índices existentes

**Actual:** [FullGetDefinition.cs:11](../SNEStorage/Services/Definitions/NewsService/FullGetDefinition.cs:11); [ResourceCommunityService.cs:120](../SNEStorage/Services/ResourceCommunityService.cs:120). Los Builds ScoreUsersBuild y LikeUsersBuild ya tienen
un índice único `(ScoreId, UserId)`. No crear otro igual.

Si los planes muestran búsquedas adicionales para leer Value, evaluar añadir
cobertura al índice que ya existe:

```csharp
entity.HasIndex(vote => new { vote.ScoreId, vote.UserId })
    .IsUnique()
    .IncludeProperties(vote => vote.Value);
```

Aplicarlo en el Build correspondiente. Mantiene la unicidad por usuario/score y
permite que Count/Average y el voto propio puedan leer Value desde el índice.
No garantiza evitar todos los scans: depende del plan y la distribución.

Las sumas, promedios y counts en IQueryable se ejecutan en SQL; no se descargan
miles de votos al navegador para calcularlos. No hace falta introducir contadores
persistidos, cachés o triggers adicionales antes de medir este coste.

## 8. Reducir la respuesta usada para verificar la sesión, si sigue costosa

**Actual:** [AccountService.cs:339](../SNEStorage/Services/AccountService.cs:339) y [AccountService.cs:742](../SNEStorage/Services/AccountService.cs:742). GetUserInfo usa cuatro relaciones de referencia,
no cuatro colecciones: esos Includes no causan una explosión cartesiana por sí
mismos. ValidateJwt ya selecciona solo SecurityStamp; eliminar esa validación
cambiaría la revocación de sesiones y no es el arreglo propuesto.

Si `/api/profile/me` sigue apareciendo como costoso, definir un DTO privado mínimo
para las comprobaciones comunes: UserId, username, color, avatar, cumpleaños
privado y roles necesarios. La página de perfil completo podría seguir con su
respuesta de detalles. Mantener email/birthday con su privacidad actual.

Antes de crear otro endpoint, medir cuánto pesan y cuánto tardan perfil, [AccountService.cs:742](../SNEStorage/Services/AccountService.cs:742) y
UserNameDTO.FromUserInfo. El frontend ya comparte la petición de sesión.

## Cosas que ya están bien y no hay que rehacer

- GetAuthors y GetDependencies consultan por lote; GetTags también hace una
  consulta por los nombres normalizados. No se propone volver a arreglar el N+1 antiguo.
- GetTypes usa el catálogo de CacheService.
- IFullGet pagina antes de GetSelect y se añade desempate por Id.
- GetResourceLastVersion usa OrderBy/ThenBy/Take(1), sin materializar todas las versiones.
- Downloads se incrementa con ExecuteUpdateAsync; mantenerlo, sin cargar/guardar
  una entidad para sumar uno.
- Los agregados de scores/likes se resuelven en SQL.
- El frontend mantiene pendiente el historial hasta abrir su tab.

## Referencias de implementación

Para comprobar SQL, proyecciones, índices y el coste de viajes de consulta:
[consultas eficientes de EF Core](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying).
Las propuestas de contratos de este documento todavía **no están conectadas**:
el frontend actual sigue funcionando con las acciones existentes.
