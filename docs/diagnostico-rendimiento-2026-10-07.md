# Diagnóstico de rendimiento de SNEStorage

Fecha: 2026-10-07. Revisión del código y de la consola proporcionada por el usuario.
No se ejecutaron pruebas, consultas a la base de datos ni cambios en código de la aplicación.

## 1. Lo que demuestra el log

Fuente: `C:/Users/HP/.codex/attachments/fd4cf2f6-a697-4bfd-a284-ea9a38a0e8ac/Texto pegado.txt`.

| Dato | Resultado |
|---|---:|
| Comandos SQL registrados | 76 |
| Suma de sus duraciones | 52.391 ms |
| Consulta de detalle, línea 331 del log | 25.248 ms |
| Consulta de detalle, línea 406 del log | 25.087 ms |
| Porcentaje acumulado de esas dos consultas | 96,1 % |
| El mismo texto SQL, línea 716 del log | 92 ms |
| Comandos de escritura de creación, líneas 254, 275 y 282 | 71 + 112 + 348 = 531 ms |
| Consultas de perfil `user_info` | 26; 546 ms acumulados |
| Advertencias EF 20504, múltiples colecciones en SingleQuery | 6 |
| Entradas `fail:` | 0 |

Las tres consultas de detalle tienen el mismo texto SQL (10.775 caracteres), con parámetros ocultos. Eso no demuestra que sus valores o sus planes sean idénticos. El log tampoco contiene tiempos HTTP completos, identificadores de petición ni marcas de tiempo para reconstruir toda la navegación. La suma de tiempos de comandos no equivale al tiempo de espera del usuario: puede haber concurrencia y trabajo fuera de SQL.

Los 531 ms corresponden a los comandos de escritura visibles, no al POST completo: faltan recepción multipart, copias físicas, materialización, serialización y transporte.

## 2. Espera después de crear: hay una segunda carga completa antes de mostrar éxito

Referencias:

- [CreateResource.razor.cs:258](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Components/Pages/CreateResource.razor.cs:258): después del POST navega a `/Resource/Created/{id}`.
- [ResourceCreated.razor.cs:6](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Components/Pages/ResourceCreated.razor.cs:6): exige `ResourceManagement = true`.
- [PageAccessGuard.razor.cs:79](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Components/Shared/PageAccessGuard.razor.cs:79): primero consulta `/api/profile/me`.
- [PageAccessGuard.razor.cs:96](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Components/Shared/PageAccessGuard.razor.cs:96): después consulta `/api/resource/{id}` para determinar acceso al manejo.
- [ResourceCreated.razor.cs:43](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Components/Pages/ResourceCreated.razor.cs:43): al mostrarse éxito, espera otros 3 segundos antes de navegar al detalle.
- [ResourceDetails.razor.cs:230](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Components/Pages/ResourceDetails.razor.cs:230): el detalle vuelve a consultar el recurso.

Flujo actual:

```text
POST crear -> verificar perfil -> descargar recurso completo para permisos
-> mostrar éxito -> esperar 3 s -> descargar recurso completo para el detalle
```

El mensaje «Checking access» procede del frontend y puede estar esperando el recurso, no necesariamente el perfil. Este flujo explica por qué un guardado ya terminado se percibe como una subida todavía bloqueada. Las dos consultas lentas son compatibles con este flujo, pero el log no tiene trazas HTTP para asignarlas inequívocamente a cada GET.

Recomendación frontend: mantener comprobación de sesión en la página de éxito, pero no descargar allí todo el recurso para comprobar permisos de edición. La pantalla solo comunica éxito y enlaza al recurso; el GET de detalle debe seguir comprobando visibilidad en backend. Para editores, usar una respuesta mínima de acceso o reutilizar la carga completa que realmente necesita el editor. El retraso de 3 segundos fue solicitado y es independiente del problema SQL.

## 3. Consulta de detalle con demasiadas colecciones en el mismo SQL — prioridad máxima

[ResourceService.cs:434](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:434).

`Get` carga tipos, votos, versiones, previews, tags, autores, dependencias, autores externos, dependencias externas y comentarios, junto con varias entidades de usuario. EF lo convierte en una consulta con muchas uniones y un ORDER BY extenso.

Las colecciones hermanas pueden multiplicar las filas. Ejemplo hipotético: 4 tipos × 3 tags × 2 autores × 5 previews × 10 votos = 1.200 filas para un recurso; versiones y comentarios pueden aumentar esa cifra. No son las cardinalidades medidas de tu recurso.

Primera modificación backend sugerida: probar `.AsSplitQuery()` en esta consulta concreta y comparar tiempos y filas. Separar colecciones elimina ese producto entre ellas, pero añade viajes SQL y no garantiza una fotografía consistente si hay cambios concurrentes. No aplicarlo globalmente sin revisar cada caso. [Documentación EF](https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries).

También retirar del detalle el bloque `Include(ResourceComments)` de la línea 464 y la construcción de sus resúmenes de usuario: los comentarios ya se solicitan mediante su endpoint paginado en `ResourceCommentsPanel`. Actualmente se cargan primero todos en el recurso y luego otra vez mediante paginación.

Conservar datos realmente usados: versiones y su File, previews y su File, nombres/colores de autores, categorías, tags y dependencias. Cambiar estrategia de consulta no implica quitar datos que la interfaz necesita.

## 4. Los 25 segundos necesitan plan y esperas para conocer su causa exacta

La misma forma de SQL también aparece con 92 ms. Hay que distinguir parámetros/cardinalidades diferentes, bloqueo, compilación/plan, CPU, E/S y esperas de memoria. No sería correcto asegurar solo con este log que toda la demora es una multiplicación de filas o que SQL Server está bloqueado.

Durante la demora, observar en SSMS (lectura, sin ejecutar cambios):

```sql
SELECT session_id, status, blocking_session_id, wait_type,
       wait_time, cpu_time, total_elapsed_time,
       reads, logical_reads, writes
FROM sys.dm_exec_requests
WHERE session_id <> @@SPID
  AND database_id = DB_ID(N'snestorage');
```

La visibilidad de todas las sesiones requiere permisos de monitorización adecuados.

Obtener el plan real de la consulta problemática con su `resourceId` y medir `SET STATISTICS IO ON` / `SET STATISTICS TIME ON`. Revisar filas estimadas/reales, lecturas, tiempo de compilación, sorts, spills y esperas. El log oculta el valor del parámetro: hay que capturarlo durante la reproducción. [Diagnóstico SQL Server](https://learn.microsoft.com/en-us/troubleshoot/sql/database-engine/performance/troubleshoot-slow-running-queries).

## 5. Comprobaciones de cuenta duplicadas — frontend y contrato mínimo

[AccountNavigation.razor.cs:45](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Components/Layout/AccountNavigation.razor.cs:45), [PageAccessGuard.razor.cs:79](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Components/Shared/PageAccessGuard.razor.cs:79), [ResourceDetails.razor.cs:119](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Components/Pages/ResourceDetails.razor.cs:119).

La barra, el guard y el detalle comprueban la cuenta por separado. Sus observadores reaccionan a navegación, foco, visibilidad y BroadcastChannel. Los límites de concurrencia son locales a cada observador, no globales. El primer resultado de una barra también anuncia un cambio aunque todavía no haya habido un cambio real desde que se abrió esa pestaña; otras pestañas pueden responder con consultas extra. No hay evidencia de un bucle infinito.

El perfil completo consultado por [AccountService.cs:129](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/AccountService.cs:129) incluye bio, redes, zona horaria, visibilidad de email, avatar y ApplicationUser. La barra solo necesita identidad, nombre, color y avatar; el guard necesita identidad y permisos.

Recomendación frontend: compartir la petición de sesión en curso, deduplicar eventos simultáneos y actualizar consumidores desde el resultado confirmado. No borrar la identidad mientras solo se está revalidando. No reutilizar perfiles de una sesión anterior ni cachear como públicos datos de usuario. Recomendación backend opcional: una acción mínima para sesión/acceso; mantener `/api/profile/me` para la vista de perfil. Las acciones de escritura siempre deben validar permisos en servidor.

## 6. Revalidar sesión puede recargar comentarios innecesariamente

[ResourceDetails.razor.cs:90](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Components/Pages/ResourceDetails.razor.cs:90) pone la identidad visual en estado anónimo durante cada revalidación. [ResourceCommentList.razor.cs:50](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Components/Resources/ResourceCommentList.razor.cs:50) vacía y recarga la lista cuando cambia esa identidad. Recuperar al mismo usuario vuelve a cambiar la clave.

Eso puede provocar cargas adicionales y pérdida de la posición del feed al volver a enfocar la ventana, aun sin login/logout real.

Recomendación frontend: distinguir «verificación en curso» de «sesión cerrada» y recargar solo cuando cambia realmente el usuario o el acceso. Durante revalidación pueden deshabilitarse mutaciones sin reconstruir toda la lista.

## 7. Permisos consultados mediante colecciones completas

[CommentService.cs:214](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:214) carga todas las versiones y autores solo para hacer `Any` después. Ocurre un patrón similar al comentar/calificar en ResourceService y al acceder a archivos.

Recomendación backend: una proyección con ID/visibilidad y booleanos calculados dentro del IQueryable usando `Any`. EF puede convertir esas comprobaciones en EXISTS. No hacen falta las colecciones materializadas para una decisión de permiso. En ediciones sí cargar las relaciones que se modificarán.

## 8. Votos y respuestas completos por comentario

[CommentService.cs:255](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:255) carga todos los LikesUsers y Childrens de cada comentario. Aunque se pidan 20 comentarios, el tamaño de sus colecciones no está acotado. Son otras dos colecciones hermanas que pueden producir multiplicación de filas.

[ResourceService.cs:447](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:447) carga todos los votos del recurso; [ResourceCommentCard.razor.cs:44](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Components/Resources/ResourceCommentCard.razor.cs:44) recuenta votos durante renderizados.

Recomendación backend: DTO de comentario con Likes, Dislikes, MyVote, HasReplies/ReplyCount y el resumen del autor. DTO del recurso con promedio, cantidad y voto propio. Los agregados deben estar en el IQueryable y ejecutarse en SQL; no cargar votantes para contarlos en C#. Pedir respuestas únicamente al expandirlas. Adaptar frontend al contrato resultante.

Es un riesgo de crecimiento confirmado por el código; las consultas paginadas de comentarios registradas ahora son de 2–42 ms, no explican las dos esperas de 25 segundos.

## 9. JSON completo atraviesa el navegador y vuelve por SignalR

[profile.js:24](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/wwwroot/js/profile.js:24): `response.text()` se incluye como string en otro JSON. Blazor Server recibe esa respuesta por JS interop y deserializa la envoltura y el cuerpo; después vuelve a enviar el resultado visual al navegador. No hay que cambiar a acceso directo a servicios: mantener controladores, pero reducir tamaño y viajes. [Coste de JS interop](https://learn.microsoft.com/en-us/aspnet/core/blazor/performance/javascript-interoperability?view=aspnetcore-10.0).

`IgnoreCycles` evita determinadas referencias circulares al serializar, pero no transforma el grafo en una respuesta pequeña ni elimina las columnas SQL cargadas antes de serializar. Los usuarios tienen JsonIgnore: ver PasswordHash en el SQL demuestra lectura de columnas, no demuestra que ese campo se envíe por JSON.

[ResourceController.cs:148](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Controllers/ResourceController.cs:148) devuelve el Resource creado entero. El frontend solo necesita su ID para continuar. Devolver `{ id }` reduce serialización, tamaño de respuesta y JS interop.

Además, no se configura un límite distinto para mensajes entrantes de SignalR en Program: el predeterminado documentado es 32 KB. Una respuesta JSON de interop grande puede superar ese límite. No hay evidencia de que ocurriera en esta sesión; se debe mirar consola del navegador y tamaño de mensajes. Reducir payload antes de ampliar límites. [SignalR en Blazor](https://learn.microsoft.com/en-us/aspnet/core/blazor/fundamentals/signalr?view=aspnetcore-10.0#maximum-receive-message-size).

## 10. Fetch sin plazo propio y solicitudes de páginas antiguas

[profile.js:8](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/wwwroot/js/profile.js:8) permite abortar por clave pero no fija un timeout HTTP propio. La barra sí tiene un plazo de 10 segundos; el guard y el detalle usan otro transporte.

En [PageAccessGuard.razor.cs:40](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Components/Shared/PageAccessGuard.razor.cs:40), cambiar de ruta invalida el resultado mediante generación pero no cancela inmediatamente la petición anterior. `_checking` impide iniciar la nueva hasta que termine la vieja. Un GET lento de la ruta anterior puede retrasar la comprobación de la siguiente.

Recomendación frontend: cancelar verificaciones obsoletas al cambiar de página, aislar su estado por generación y aplicar plazos/reintento explícitos a lecturas. Esto evita esperas engañosas, pero no acelera la consulta SQL. No repetir automáticamente un POST de creación cuyo resultado se perdió: puede estar ya guardado.

## 11. Consultas individuales dentro de bucles de creación

[ResourceService.cs:781](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:781): una consulta por tipo.
[ResourceService.cs:801](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:801): una por dependencia.
[ResourceService.cs:832](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:832): una por autor.
[ResourceService.cs:865](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:865): una por tag.

Recomendación backend: consultar cada grupo mediante una lista de IDs/nombres, proyectar solo lo necesario y usar diccionarios para asociar resultados; crear tags que falten. Normalizar las claves con la semántica de búsqueda deseada. No intentar paralelizar estas consultas con Task.WhenAll sobre el mismo DbContext. [Consultas eficientes EF](https://learn.microsoft.com/en-us/ef/core/performance/efficient-querying).

## 12. Subida: red, buffer temporal, copia física y manifiesto

[ResourceController.cs:144](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Controllers/ResourceController.cs:144) recibe IFormFile mediante FromForm. Los archivos se bufferizan antes de la acción; por defecto, sobre 64 KB se usa un temporal en disco. [Subidas ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/mvc/models/file-uploads?view=aspnetcore-10.0).

[FileService.cs:77](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/FileService.cs:77) vuelve a copiar al almacenamiento definitivo. [FileService.cs:52](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/FileService.cs:52) consulta MIME por archivo y UpdateBatch lee/deserializa/rewrite el manifiesto para cada archivo. GetPreviews procesa archivos secuencialmente.

Estos costes no tienen tiempos en el log. Con archivos grandes hay que medir recepción, temporales, copia y antivirus/E/S. Streaming es una opción si buffering consume demasiados recursos; no promete acelerar mágicamente la subida ni exige abandonar el rollback. Resolver MIME una vez por conjunto/request puede ahorrar consultas. Con máximo 21 archivos, optimizar el pequeño manifiesto va después de resolver las consultas de 25 segundos.

Frontend: falta progreso de subida por bytes/fases. Eso influye en la percepción, no en la velocidad real. El archivo se envía como FormData por HTTP; no encontré copia del archivo de 250 MB a un MemoryStream del componente ni transporte de sus bytes por SignalR.

## 13. Obtener un archivo carga todas las versiones/previews/autores

[ResourceService.cs:490](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:490) materializa todas esas colecciones y luego busca el archivo solicitado en memoria. Download reutiliza ese método.

Recomendación backend: comprobar acceso y pertenencia, proyectar directamente el archivo solicitado y su MIME. Un preview no debería necesitar el historial completo para obtener un único archivo.

En [resource-media.js:55](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/wwwroot/js/resource-media.js:55) ya se aprovecha File del detalle para evitar el GET de metadatos cuando es suficiente, y solo se carga el preview activo. Sin embargo, el vídeo se convierte en Blob completo antes de iniciar reproducción; no aprovecha reproducción progresiva/rangos del endpoint. Cambiar de preview libera el Blob anterior y puede repetir la descarga al volver. Medir esto aparte; el límite actual de 5 MB contiene el coste por archivo.

## 14. Búsquedas, índices y caché de opciones

- [ExternalAuthorService.cs:14](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ExternalAuthorService.cs:14) devuelve todos los nombres distintos para término vacío/corto, ignorando maxAmount en esa rama. Aplicar un límite también allí.
- [author-search.js:9](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/wwwroot/js/author-search.js:9) no usa AbortController. La cancelación del debounce C# no aborta un fetch ya iniciado para autores/dependencias. Cancelar peticiones obsoletas.
- Tags usan LOWER/LTRIM/RTRIM en la columna; MIME usa LOWER. Revisar normalización/collation/columna normalizada indexada según el plan.
- Las búsquedas `%término%` de videojuegos/usuarios/recursos/autores pueden requerir recorrer muchos valores; Take(10) limita resultados, no garantiza leer solo diez filas. No cambiar a prefijo si eso altera la UX deseada sin decidirlo.
- El snapshot ya contiene índices de FK. No afirmar que faltan todos los índices ni crear duplicados: revisar los índices reales en SQL Server. Candidatos a evaluar: votos `(ScoreId, UserId)` y relaciones/orden de comentarios según el plan. Los agregados/orden por votos merecen medición con volúmenes reales.
- [ResourceController.cs:69](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Controllers/ResourceController.cs:69) consulta tipos y visibilidades cada vez. Las opciones de registro hacen algo similar. Son buenos candidatos para una caché pequeña con invalidación al modificar catálogos; no fueron el coste principal de este log.

## 15. Crecimiento del feed y cálculos de presentación

[ResourceCommentList.razor.cs:126](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Components/Resources/ResourceCommentList.razor.cs:126) conserva todos los comentarios cargados en memoria y DOM. Los feeds de respuestas también acumulan elementos. Para sesiones largas, evaluar virtualización o una ventana de elementos manteniendo posición/drafts.

Las propiedades de ResourceDetails vuelven a convertir/sortear versiones, categorías y previews en distintos accesos durante cada render; las tarjetas cuentan votos varias veces. Después de reducir payload, precalcular presentación al recibir/cambiar datos. El observer de comentarios también manda `update` por interop en cada render aunque sus argumentos no cambien. Evitar llamadas repetidas iguales. Son mejoras secundarias; no explican por sí solas el SQL de 25 segundos.

## 16. Observabilidad y entorno de desarrollo

[DBHelperService.cs:61](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/DBHelperService.cs:61) captura errores sin registrarlos. FileService y AccountService tienen catches similares. Devolver enums está bien, pero conservar en logs internos la excepción y la operación evita confundir timeout, cancelación y error de datos.

Añadir medición por petición y fase: recepción/copia de archivo, asociaciones, SaveChanges, lectura del recurso, serialización, respuesta y JS interop. En la pestaña Network distinguir el POST de `/api/resource/create/submit` de los GET posteriores. Registrar ruta sin tokens, identificador de petición, resourceId, duración y bytes; los logs SQL actuales no correlacionan cada comando con el recorrido visual.

Development registra SQL muy extenso; consola/debugger, compilación inicial, JIT y primera conexión pueden distorsionar la percepción. Comparar la misma operación con y sin depurador, primera carga y cargas siguientes, y Release. No usar ese factor como explicación automática: el propio log ya registra dos comandos muy lentos. [Diagnóstico de rendimiento EF](https://learn.microsoft.com/en-us/ef/core/performance/performance-diagnosis).

## Orden recomendado

1. Medir la consulta de detalle y evaluar AsSplitQuery local; sacar comentarios del Get del recurso.
2. Evitar el Get completo para permisos en la página de éxito; cargar el detalle una sola vez después de crear.
3. Deduplicar sesión/revalidaciones y no recargar comentarios por una verificación del mismo usuario.
4. Proyectar permisos, agregados y respuestas mínimas; reducir grafos y tamaño por SignalR.
5. Consultas por lotes para asociaciones y archivo individual; límites/cancelación de búsquedas.
6. Medir subida por fases, índices reales y crecimiento del feed antes de optimizaciones menores.

Resultado esperado de los primeros pasos: eliminar una carga grande redundante y reducir el coste de la que sí necesita la página individual. No se puede prometer un tiempo final sin medición posterior.
