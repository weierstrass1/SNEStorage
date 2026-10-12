# Revisión actual de servicios, controladores y conexión con el frontend

Actualizado: **10 de octubre de 2026**. Este documento contiene únicamente pendientes comprobados en el código actual y decisiones todavía pendientes. Los puntos solucionados se retiraron; los parcialmente corregidos se redujeron a lo que falta.

## Alcance y comprobación

Se revisaron AccountService, ResourceService, CommentService, FileService, CacheService, DBHelperService, CaptchaService, CaptchaFrequencyService, EmailService, DateTimeService, APIConfig, UserService, VideogameService y ExternalAuthorService; sus controladores, los DTOs y los mappings EF relacionados. Se contrastaron los contratos relevantes con el frontend.

Esta actualización es por lectura de código. **No se ejecutaron tests, subidas, peticiones contra la aplicación ni consultas a SQL Server.** No se comprobó qué migraciones están aplicadas ni la configuración del hosting. La compilación anterior terminó con 0 errores y 5 advertencias; no se volvió a compilar para editar este documento.

Solo se modificó este documento. Las propuestas de backend son para que las apliques tú. Las líneas enlazadas corresponden a los archivos revisados y pueden desplazarse con cambios posteriores.

## Orden de trabajo

- **P1 — Alta:** fallos en operaciones válidas o requisitos antes de publicar. Los requisitos de publicación se indican expresamente; no son fallos del entorno de desarrollo.
- **P2 — Media:** integridad de datos, permisos contextuales, contratos incompletos y consultas que cargan información innecesaria.
- **P3 — Baja:** clasificación de cancelaciones y presentación.

Dentro de cada prioridad, seguir el orden de la tabla. Las observaciones de rendimiento son candidatas por lectura de código; no son tiempos medidos.

| Orden | Prioridad | Pendiente | Área |
|---|---|---|---|
| 1 | P1 | Extraer y conservar los errores del registro | AccountController / RegisterController |
| 2 | P1 | Alinear la capacidad de release notes con el límite admitido | ResourceVersion.Build / ResourceService |
| 3 | P1, antes de publicar | Validar archivos según su uso | FileService / AccountService / ResourceService |
| 4 | P1, antes de publicar | Configurar límites HTTP de peticiones | Program / controladores |
| 5 | P1, antes de publicar | Usar un transporte de correo real | EmailService |
| 6 | P2 | Eliminar Score y votos al borrar físicamente un comentario | CommentService |
| 7 | P2 | Completar la protección frente a escrituras concurrentes | CommentService / ResourceService / mappings |
| 8 | P2 | Comprobar acceso antes de consultar el voto propio | CommentService |
| 9 | P2 | Distinguir comentarios de recursos de otros contextos | CommentService |
| 10 | P2 | Manejar fallos concretos del registro fuera de su catch actual | AccountService / EmailService |
| 11 | P2 | Completar avatar y color en la respuesta inmediata de Reply | CommentService / AccountService |
| 12 | P2 | Proyectar permisos al leer o publicar comentarios | CommentService / ResourceService |
| 13 | P2 | Reducir la carga de relaciones al añadir o editar versiones | ResourceService |
| 14 | P2 | Conservar los errores específicos del perfil | ProfileController / AccountService |
| 15 | P2 | Completar validaciones de relaciones y campos auxiliares | ResourceService / DTOs |
| 16 | P2 | Validar datos del perfil en backend | AccountService / EditProfileDTO |
| 17 | P3 | Leer el contexto HTTP correcto al clasificar cancelaciones | FileController |
| 18 | P3 | Ordenar los autores de A a Z | ResourceService |

## P1 — Alta

### 1. Extraer y conservar los errores del registro

**Ubicaciones:** [AccountController.Register, línea 18](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Controllers/AccountController.cs:18) y [RegisterController.Register, línea 50](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Controllers/RegisterController.cs:50).

**Código pendiente:** `return BadRequest(error);`.

Aquí error es `OneOf<AccountServiceResult, FileServiceResult>`. Se entrega el contenedor, no su alternativa activa. La serialización predeterminada puede leer propiedades de una alternativa inactiva y lanzar una excepción. No se reprodujo mediante una petición; la observación deriva del código y de los getters de [OneOf](https://github.com/mcintyre321/OneOf/blob/master/OneOf/OneOfT2.generated.cs).

**Propuesta:** añadir el mapper de AccountServiceResult con el formato `AccountController-NombreDelError`, recibir FileController mediante DI y extraer la alternativa activa:

```csharp
if (res.TryPickT0(out var response, out var error))
    return response;

return error.TryPickT0(out var accountError, out var fileError)
    ? GetActionResult(accountError)
    : _fileController.GetActionResult(fileError);
```

El mapper debe distinguir DBError/Error de errores de entrada: los primeros corresponden a un fallo de servidor. El mapper de archivos ya contiene sus estados específicos.

RegisterController convierte cualquier fallo en `/Register?result=failed`. Conservar el error que el frontend sabe mostrar:

```csharp
var response = await _accountController.Register(credentials);
if (response.Value is not null)
    return Redirect("/Register/CheckEmail");

return response.Result!;
```

Para admitir multipart directamente en `/api/account/register`, declarar `[FromForm] RegisterInfo credentials`. RegisterController ya utiliza FromForm; una llamada directa entre controladores no vuelve a ejecutar el model binding ni los filtros del segundo controlador.

**Frontend:** ya interpreta errores prefijados de cuenta/archivo y el redirect exitoso. Falta que el backend entregue los errores.

### 2. Alinear la capacidad de release notes con el límite admitido

**Ubicaciones:** [ResourceVersion.Build, línea 13](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Models/Builds/ResourceVersionBuild.cs:13), [Create, línea 731](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:731), [AddVersion, línea 489](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:489) y [EditVersion, línea 569](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:569).

**Código actual:** el servicio admite `MAX_DESCRIPTION_SIZE = 10000`, pero el mapping usa:

```csharp
entity.Property(e => e.ChangeLog)
    .HasColumnName("change_log")
    .HasColumnType("nvarchar(2048)");
```

Una nota de 2.049–10.000 caracteres supera la validación y no cabe en ese esquema. Puede terminar como DBError al guardar. No se inspeccionó la columna real de SQL Server.

**Propuesta:** mantener el máximo de 10.000, cambiar el mapping a `nvarchar(max)` y aplicar la migración que amplíe la columna. `nvarchar(10000)` no es una longitud fija válida en SQL Server. Si prefieres 2.048, reducir el límite en servicio y frontend.

**Frontend:** admite 10.000; solo requiere cambio si decides reducir el contrato.

### 3. Validar archivos según su uso

**Ubicaciones:** [FileService.ValidateFile, línea 183](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/FileService.cs:183), [AccountService.Register, línea 55](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/AccountService.cs:55), [EditUserInfo, línea 241](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/AccountService.cs:241) y [ResourceService.GetPreviews](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:920).

**Pendiente conocido de seguridad:** ValidateFile analiza imágenes, vídeos y comprimidos, pero PrepareFile no recibe el uso del archivo. Un archivo válido globalmente puede ser incorrecto para el campo donde se sube. Por ejemplo, Register/EditUserInfo pueden preparar un comprimido como avatar; el límite de 5 MB para media no se aplica a ese comprimido. Los recursos tienen límites de bytes para principal/previews, pero no una separación de formatos por campo.

**Propuesta:** proporcionar a ValidateFile/PrepareFile un contexto o comprobarlo en cada operación: avatar = imágenes permitidas; preview = imágenes/vídeos; principal = comprimidos. Aplicar el tamaño del contexto aunque el MIME pertenezca a otra familia. Conservar la inspección real de contenido ya implementada.

**Frontend:** accept y límites ayudan al usuario, pero no impiden llamar a la API con otros archivos. No hay que rehacer la inspección de contenido existente.

### 4. Configurar límites HTTP de peticiones

**Ubicaciones:** [Program](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Program.cs:121), [AccountController.Login](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Controllers/AccountController.cs:29) y [CaptchaFrequencyService](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CaptchaFrequencyService.cs:1).

**Antes de publicar:** no se encontró AddRateLimiter/UseRateLimiter ni políticas HTTP en la aplicación. El lockout de Identity limita intentos fallidos de una cuenta; CaptchaFrequencyService decide cuándo pedir captcha. Ninguno impone por sí solo un máximo de peticiones a login, búsquedas o subidas.

**Propuesta:** políticas de rate limiting para login/registro y operaciones costosas, devolviendo 429 cuando se excedan; considerar también concurrencia máxima para subidas. La protección de proxy/hosting frente a ataques volumétricos sigue siendo un trabajo separado que acordaste posponer. [Middleware oficial de ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0).

**Frontend:** revisar mensajes de 429 cuando definas el contrato; no convertir el captcha en un rate limiter.

### 5. Usar un transporte de correo real

**Ubicación:** [EmailService.Confirmation, línea 74](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/EmailService.cs:74).

**Antes de publicar:** sigue conectando a `sandbox.smtp.mailtrap.io`. Es el transporte de desarrollo; no se probó entrega al correo real del usuario en esta revisión.

**Propuesta:** configurar host, puerto, credenciales y seguridad del transporte de producción. Mantener el template y comprobar entrega antes de publicar: la aplicación exige email confirmado para autenticarse.

**Frontend:** sin cambios de contrato.

## P2 — Media

### 6. Eliminar Score y votos al borrar físicamente un comentario

**Ubicación:** [CommentService.DeleteComment, línea 432](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:432).

**Pendiente:** sin hijos ejecuta únicamente `_context.Comments.Remove(comment)`. Comment tiene la FK ScoreId: borrar el dependiente no borra el principal Score. Pueden quedar Score/LikesUsers sin comentario. El cascade actúa al borrar Score. [Dirección de cascadas en EF Core](https://learn.microsoft.com/en-us/ef/core/saving/cascade-delete).

**Propuesta:** eliminar también su Score en el mismo SaveChanges, manteniendo Parent/ResourceComment cargados para sus asociaciones. LikesUsers tiene Cascade desde Score. Con respuestas y borrado lógico, conservar el Score según tu política.

**Frontend:** ya oculta borrados sin respuestas y conserva los placeholders con respuestas.

### 7. Completar la protección frente a escrituras concurrentes

**Ubicaciones:** [LikeUsers.Build, línea 10](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Models/Builds/LikeUsersBuild.cs:10), [CommentService.AddEditOrDeleteLike, línea 120](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:120), [ResourceService.AddVersion, línea 484](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:484) y [ValidateVersion, línea 1623](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:1623).

**Pendiente reducido:** LikeUsers no tiene índice único `(ScoreId, UserId)` en su mapping. Dos peticiones pueden ver que no existe el voto y ambas insertarlo. El índice único de ScoreUsers ya está definido y no se considera ausente.

**Propuesta para likes:** índice único en esa pareja, migración y tratamiento del conflicto al insertar: volver a leer el voto o devolver un conflicto controlado.

**Versiones:** ValidateVersion comprueba el orden para una operación aislada, pero validar/guardar están separados. Dos altas pueden aprobar contra el mismo historial. La pareja única `(ResourceId, Version)` protege etiquetas exactamente iguales, pero no garantiza el orden semántico ni reconoce `1`/`1.0` como equivalentes. Para asegurar el orden bajo concurrencia, serializar validación y escritura por recurso mediante una estrategia transaccional de base de datos.

No se comprobó el estado de los índices en SQL Server. Un conflicto de unicidad de ScoreUsers puede convertirse en DBError mediante el helper; mejorar ese resultado no es lo mismo que añadir la restricción, que ya existe en el modelo.

**Frontend:** deshabilitar botones evita dobles clics comunes, pero no sustituye la protección del servidor.

### 8. Comprobar acceso antes de consultar el voto propio

**Ubicación:** [CommentService.GetUserLike, línea 319](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:319).

**Pendiente:** exige usuario conectado, pero consulta su voto sin verificar acceso al comentario. Si el recurso pasó a privado y perdió acceso, el endpoint sigue respondiendo sobre el voto cuando exista.

**Propuesta:** comprobar acceso contextual y después consultar por ScoreId/UserId. No devolver un grafo de entidades para este GET.

**Frontend:** los GET de comentarios ya incluyen MyVote; el endpoint independiente también necesita permisos.

### 9. Distinguir comentarios de recursos de otros contextos

**Ubicación:** [CommentService.CheckResourcePermissionFromComment, línea 169](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:169).

**Pendiente:** busca `c.Id == commentId && c.ResourceComment != null`. GetReplies y likes usan este helper. Tu diseño admite comentarios fuera de recursos: en ese caso devuelve CommentNotFound aunque existan. Las respuestas de recursos mantienen ResourceComment; no necesitan recorrer padres.

**Propuesta:** buscar por Id; con ResourceComment verificar recurso, sin él aplicar la política de su otro contexto. No conceder acceso automáticamente a cualquier comentario sin recurso. Si otros contextos aún no existen, es un pendiente de integración futura, no un fallo de los comentarios actuales de recursos.

**Frontend:** adaptación cuando definas esos contextos.

### 10. Manejar fallos concretos del registro fuera de su catch actual

**Ubicaciones:** [AccountService.Register, línea 59](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/AccountService.cs:59), [token/envío, línea 93](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/AccountService.cs:93) y [EmailService.Confirmation, línea 65](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/EmailService.cs:65).

BeginTransactionAsync/CreateAsync están después de preparar el avatar, fuera del catch de rollback. Un fallo excepcional puede dejar el batch pendiente. La rama IdentityResult fallido sí hace rollback: esa rama está cubierta.

Tras el commit, generar el token o cargar el recurso enlazado del correo también puede lanzar fuera del catch SMTP. El cliente podría ver fallo cuando la cuenta ya quedó guardada.

**Propuesta:** usar tu helper alrededor de esas operaciones concretas. Antes del commit, rollback del batch; después, mantener éxito del registro y registrar el fallo para permitir reenvío. No revertir un commit confirmado ni añadir un try enorme.

El resultado de Confirmation se ignora. El reenvío quedó pospuesto por tu decisión; no se propone bloquear el registro si el correo falla.

**Frontend:** el botón de reenvío necesita una acción de backend que no aparece en los controladores revisados.

### 11. Completar avatar y color en la respuesta inmediata de Reply

**Ubicaciones:** [CommentService.AddCommentToComment, línea 371](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:371), [AccountService.GetUser, línea 313](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/AccountService.cs:313) y [UserNameDTO.FromUser](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/DTOs/UserNameDTO.cs:40).

Reply genera UserSummary mediante FromUser; GetUser usa FindAsync sin cargar UserInfo/Avatar. Si esas relaciones no estaban cargadas, la respuesta inmediata puede traer solo ID/nombre; los GET de CommentReadDto sí proyectan color/avatar.

**Propuesta:** consultar/proyectar el resumen completo para Reply, sin hacer que todos los usos de GetUser carguen relaciones. Mantener UserNameDTO.

**Frontend:** ya utiliza esos campos cuando llegan; no puede inventar los ausentes.

### 12. Proyectar permisos al leer o publicar comentarios

**Ubicaciones:** [CommentService.CheckResourcePermission, línea 214](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:214) y [ResourceService.AddComment, línea 158](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:158).

**Rendimiento pendiente:** cargan ResourcesAuthors/ResourceVersions completos para comprobar coincidencias. CheckResourcePermission lo hace antes de cada página de comentarios/respuestas; el DTO de GetEnd no elimina esa carga previa.

**Propuesta:** proyectar VisibilityId y un booleano con Any para autores/submitters, más acceso del staff. AddComment puede usar GetAccess/CheckViewAccess existentes y crear ResourceComments por ResourceId. CommentService necesita su consulta de acceso o un helper de servicio compartido que definas; los helpers de ResourceService son privados.

No consultar por autor/versión ni ejecutar consultas simultáneas sobre el mismo DbContext. Los Includes UserInfo/Avatar de AddComment sirven para su resumen actual.

**Frontend:** contrato sin cambios.

### 13. Reducir relaciones al añadir o editar versiones

**Ubicaciones:** [ResourceService.AddVersion, línea 492](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:492), [EditVersion, línea 548](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:548) y [GetMainResourceFolder, línea 1132](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:1132).

AddVersion carga todas las versiones/File y autores; EditVersion carga todas las versiones/autores. ValidateVersion ya consulta el historial mínimo Id/Version por separado.

**Propuesta:** comprobar permisos con GetAccess/CheckEditAccess. En AddVersion obtener una sola URL para derivar la carpeta y crear por ResourceId. En EditVersion cargar con seguimiento solo la versión por ResourceId/Id. Mantener ValidateVersion para el orden. AsSplitQuery puede mitigar temporalmente las colecciones, pero no elimina los datos innecesarios.

**Frontend:** contrato sin cambios.

### 14. Conservar los errores específicos del perfil

**Ubicaciones:** [ProfileController.GetMyProfile, línea 19](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Controllers/ProfileController.cs:19), [EditMyProfile, línea 34](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Controllers/ProfileController.cs:34) y [AccountService.EditUserInfo, línea 217](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/AccountService.cs:217).

GetUserInfo devuelve Error cuando falla la consulta, pero el mapper lo convierte en BadRequest sin cuerpo. EditUserInfo devuelve bool y descarta FileServiceResult; un avatar inválido termina en mensaje genérico.

**Propuesta:** fallos de servidor = 500 con error prefijado; conservar 499 para cancelación explícita. Para edición, devolver AccountServiceResult/FileServiceResult y mapearlos como en ResourceController. No cambiar el DTO de entrada por este motivo.

**Frontend:** reconoce los prefijos y cuerpos de error; adaptar solo si cambia también el éxito.

### 15. Completar validaciones de relaciones y campos auxiliares

**Ubicaciones:** [ResourceService.GetTypes, línea 976](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:976), [GetExternalDependencies, línea 960](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:960), [GetExternalAuthors, línea 1035](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:1035) y [Create, línea 715](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:715).

**Solo lo que falta:**

- GetTypes itera IDs sin Distinct. Create no deduplica después y la relación no tiene una pareja única en el mapping; una API puede generar relaciones repetidas. Edit sí deduplica su colección final.
- No se comprueba explícitamente VideogameId/VisibilityId antes de guardar: una FK inválida acaba como DBError. También afecta a Edit. Los tipos/autores/dependencias sí tienen comprobación de existencia.
- GetExternalDependencies copia nombres/URLs/mirrors sin formato ni longitudes. Su mapping permite nombre de 100 y URLs/mirrors de 255. GetExternalAuthors no valida blanco/longitud por nombre; la columna admite 200.
- El máximo de 50 elementos no limita la longitud de cada cadena. En Create, el autor añadido por IAmAuthor también debe contarse cuando no estaba en AuthorsIDs.

**Propuesta:** deduplicar typeIds; validar Visibility con caché y Videogame con consulta pequeña; validar campos auxiliares según columnas y esquemas de URL permitidos. Mantener las validaciones generales y consultas por lote implementadas.

El punto 2 cubre ChangeLog. No vuelven a incluirse los límites de nombre/repositorio/versión ni rangos del buscador que ya se corrigieron.

**Frontend:** las llamadas directas requieren estas comprobaciones del servicio/DTO.

### 16. Validar datos del perfil en backend

**Ubicaciones:** [AccountService.EditUserInfo, línea 217](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/AccountService.cs:217), [GetSocialNetwork, línea 322](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/AccountService.cs:322), [EditProfileDTO](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/DTOs/EditProfileDTO.cs:1) y [UserInfo.Build](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Models/Builds/UserInfoBuild.cs:33).

GetSocialNetwork distingue null/blanco/valor, pero no valida formato/host por red. Las columnas de enlaces admiten 100 y UsernameColor 7; el servicio no valida esas restricciones. TimeZoneId/EmailVisibilityId tampoco se comprueban contra opciones permitidas. El frontend sí valida enlaces y colores para su presentación.

**Propuesta:** validadores por campo; para URLs, Uri/host permitido además del formato de usuario que admitas. Respetar `null = sin cambios` y `"" = quitar valor`. Excluir ResourceOnly en email visibility; usar caché para timezone/visibility.

**Frontend:** conservar sus mensajes/normalización y hacer que backend admita el mismo contrato.

## P3 — Baja

### 17. Leer el contexto HTTP correcto al clasificar cancelaciones

**Ubicación:** [FileController.GetActionResult, línea 35](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Controllers/FileController.cs:35).

ResourceController llama a FileController como dependencia; el segundo no recibe automáticamente el ControllerContext de esa petición. `HttpContext?.RequestAborted` puede no estar disponible y CancellationTokenError terminar en 500 en vez de 499.

**Propuesta:** IHttpContextAccessor o pasar el estado de cancelación al mapper desde el controlador de la petición. El `?.` evita una desreferencia directa; lo pendiente es clasificar bien. No impide una subida/lectura normal.

**Frontend:** éxito sin cambios.

### 18. Ordenar los autores de A a Z

**Ubicación:** [ResourceService.GetResourceAllAuthors, línea 1227](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:1227).

**Pendiente:** `OrderByDescending(v => v.UserSummary.Username)` devuelve Z–A.

**Propuesta:** OrderBy, manteniendo ThenBy(Id). Decidir si también quieres orden ascendente en autores externos/dependencias, que usan OrderByDescending.

**Frontend:** sin cambios; presentación únicamente.

## Decisiones y mantenimiento pospuestos

Separados del orden inmediato porque dependen de tus decisiones o del despliegue:

- **Dependencias privadas/ciclos:** GetDependencies comprueba existencia, pero queda decidir acceso y cambios de visibilidad de dependencias. Mantener lo acordado de discutirlo después. La exclusión de dependencia del propio recurso en Edit ya está aplicada y no se lista como pendiente.
- **Seal of Quality:** implementar las restricciones definitivas de edición/visibilidad/borrado descritas en las reglas. Edit/EditVersion permiten editar al contribuidor aunque exista el sello; definir cómo se autoriza el permiso del staff antes de limitarlo.
- **Descargas/rangos:** ya se incrementa el contador. Definir si rangos/reanudaciones cuentan otra descarga; no se probó en navegador. No se lista una falta de incremento.
- **Manifiestos/archivos huérfanos:** mantener tu limpieza periódica. FinishDeletion ya trata errores por archivo y conserva el éxito tras commit; no se propone otro try. Los manifiestos retenidos tras un cleanup fallido pertenecen a este mantenimiento, distinto del flujo del punto 10.
- **Diagnóstico de fallos:** DBHelperService/FileService devuelven errores/default sin registrar la excepción. Si necesitas investigar DBError en producción, añadir logs locales sin tokens/passwords ni datos sensibles; no exponer detalles al cliente.
- **Medición:** las acciones de sección consultan acceso por separado para proteger cada endpoint. Medir antes de rediseñarlas. SelectFind/Get tienen split; no se afirma que falte ni se dan mejoras de tiempo sin medir.
- **SQL Server/hosting:** verificar al publicar las migraciones de índices/longitudes y el límite de subida del hosting/proxy. ResourceController ya define límites HTTP para 250 MB; no se listan como ausentes.
- **Advertencias existentes:** la compilación anterior reportó `fixtexts` y propiedades no inicializadas de ResourceCategory/MemoryAddressValues. No bloqueaban compilación y no se modificaron.
