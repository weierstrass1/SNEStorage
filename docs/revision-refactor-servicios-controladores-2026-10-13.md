# Revisión de la refactorización — 13 de octubre de 2026

## Alcance y resultado

Se leyeron los controladores actuales, los servicios Account, Resource, Comment,
File, News, Cache, Captcha, Discord, Email, DateTime y búsquedas, las interfaces
refactorizadas, los contratos ServiceResult y los mappings relacionados con los
hallazgos. Se contrastaron rutas y payloads con los clientes del frontend.

**No se modificó backend.** Los cambios de Program.cs indicados durante la revisión
los aplicó el propietario. Los cambios realizados por el asistente son de frontend
y documentación.

- La aplicación web compila: **0 errores, 5 advertencias existentes**.
- El bot también compiló durante la compilación de la solución.
- La solución completa falla por **3 errores en el proyecto de tests**, detallados abajo.
- No se ejecutaron tests, peticiones contra producción, migraciones ni operaciones
  reales de registro/subida/borrado. Los hallazgos de lógica se basan en el código
  y mappings; no se certifica que todas las queries se traduzcan en la DB desplegada.
- La demostración de rutas del punto 1 solo calculó una ruta en memoria: no creó
  ni modificó archivos.

Las ubicaciones corresponden al estado leído hoy. Los puntos incluyen problemas
actuales anteriores a esta refactorización; no todos son regresiones nuevas.

## Adaptación del frontend realizada

1. `CommentApiClient`: sustitución de las dos referencias al enum eliminado
   CommentServiceResult por ServiceResultTypes. Se conserva el tratamiento de
   LikeNotFound al comprobar/remover un voto.
2. `ApiErrorPresentation`: tratamiento de errores compartidos de archivos/email
   aunque los devuelva ResourceController, ProfileController o AccountController.
   El prefijo identifica el endpoint; ya no permite deducir qué servicio produjo
   el error. Se añadieron InvalidFileForContext y variantes de paginación, y se
   conserva la compatibilidad con cuerpos antiguos.
3. Mensajes de avatar ajustados a formatos admitidos; las respuestas de error de
   ProfileController se reconocen como perfil.
4. Respuestas **204 No Content** aceptadas en edición de recursos/versiones,
   edición/borrado de comentarios, reset/reenvío de emails. Crear una versión
   sigue requiriendo el objeto con IDs; no se acepta un 204 como versión creada.
5. El editor de recursos conserva la diferencia 401/403 para mostrar el error
   y la opción de login apropiados.
6. El perfil público pasa el cuerpo del error a ErrorPanel y un perfil inexistente
   no invita a iniciar sesión como si hacerlo fuese a recuperar ese perfil.

Las rutas HTTP existentes **no cambiaron** en la refactorización observada. No fue
necesario inventar endpoints ni sustituir rutas que siguen siendo válidas. Register
ya recibe RegisterResponseDTO por JSON, noticias conservan el ID como éxito,
comentarios sus DTOs/relaciones y descarga sigue retornando bytes. No quedan usos
de los enums eliminados en los componentes/clientes JavaScript revisados.

## Compilación ya corregida durante esta revisión

Program.cs, OnTokenValidated: ValidateJwt ahora devuelve
OneOf<bool, ServiceResultContext> y BuildToken devuelve
OneOf<AuthenticationResponse, ServiceResultContext>. Las comprobaciones antiguas
con `!await` y `== null` quedaron sustituidas por TryPickT0. El frontend ya no usa
CommentServiceResult.

Estos errores de compilación están resueltos. La distinción entre fallo de DB y
token inválido requiere además el punto 2.

## P1 — Prioridad alta

### 1. El username del registro puede alterar la ruta del avatar

**Clase/métodos:** AccountService.Register y GetAvatarUrl; FileService.PrepareFile.

**Ubicaciones:**

- [Register: construcción y preparación del avatar](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/AccountService.cs:152).
- [GetAvatarUrl: nombre del archivo](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/AccountService.cs:771).
- [PrepareFile: creación física](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/FileService.cs:134).

El nombre del archivo incluye username, procedente del formulario. Identity
valida ese username en CreateAsync **después** de PrepareFile. Ni RegisterInfo
ni el servicio rechazan separadores de ruta antes de esa operación.

Con un username como `../../../../images/site-logo`, la combinación conceptual
`wwwroot/Content/Avatars/upload_user_../../../../images/site-logo.png` normaliza
fuera de Avatars. Si el destino existe, cambiar el GUID no cambia la ruta final
normalizada y el `while (File.Exists(...))` puede repetirse indefinidamente.
Si no existe, se puede escribir temporalmente fuera de la carpeta prevista antes
de que Identity rechace el registro. El rollback posterior no vuelve seguro ese
procesamiento anticipado.

**Corrección simple propuesta:** quitar username del nombre físico:

```csharp
physicalPath = Path.Combine(_environment.WebRootPath,
    "Content", "Avatars", $"{Guid.NewGuid():N}_{userId}{extension}");
```

Eliminar también el parámetro username si deja de usarse. Como defensa adicional,
FileService puede comprobar la ruta normalizada contra la carpeta permitida para
el contexto y crear con FileMode.CreateNew para evitar sobrescrituras por carrera.
La corrección del nombre no necesita un try/catch grande ni una DB adicional.

### 2. Un fallo de validación por DB se presenta como JWT inválido

**Clase/método:** Program.cs, OnTokenValidated; AccountService.ValidateJwt.

**Ubicaciones:** [Program.cs](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Program.cs:83),
[ValidateJwt](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/AccountService.cs:721).

La comprobación nueva agrupa IsT1 y bool=false y ejecuta context.Fail en ambos.
Por eso una excepción/timeout de la DB al consultar SecurityStamp puede acabar
como 401 de una acción Bearer. El cliente de sesión interpreta ese 401 como sesión
ausente y guarda el estado anónimo: el usuario parece perder login aunque su JWT
siga siendo válido. La cookie no se borra por esta comprobación.

**Propuesta:** separar bool=false, que corresponde a 401, del error del servicio,
que debe dar 503 o el estado de cancelación. No permitir el acceso cuando falla
la DB. Se puede guardar el error en HttpContext.Items, fallar la autenticación y
usar OnChallenge para `HandleResponse()` y devolver el estado/cuerpo de fallo del
servicio. No basta con poner StatusCode antes de context.Fail, porque el challenge
puede reemplazarlo.

El TryPickT0 indicado inicialmente arregla la compilación y falla de forma cerrada;
esta distinción adicional evita confundir una indisponibilidad con un logout.

### 3. Descartado: sugerencias compartidas de autores externos

El 14 de octubre de 2026, el propietario confirmó que las sugerencias de autores
externos deben compartirse entre todos los recursos, incluidos los privados.
La reutilización de nombres es el comportamiento deseado. Se acepta que un nombre
pueda aparecer aunque solo esté asociado a recursos privados; no se publica el
recurso ni su contenido mediante esta búsqueda. Este punto no requiere corrección.

## P2 — Prioridad media

### 4. Faltan validaciones backend del perfil y opciones del registro

**Clase/métodos:** AccountService.EditUserInfo y Register.

**Ubicación:** [EditUserInfo](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/AccountService.cs:528),
[Register](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/AccountService.cs:143),
[EditProfileDTO](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/DTOs/EditProfileDTO.cs:5).

Se copian color, enlaces y IDs sin verificar sus restricciones. UsernameColor está
limitado a nvarchar(7), enlaces a nvarchar(100), y TimeZoneId/EmailVisibilityId tienen
FK. Un valor inexistente/excesivo termina como DBError; un enlace de una plataforma
incorrecta puede guardarse si cabe. El frontend valida, pero se puede llamar la API
directamente. Register también acepta IDs de visibilidad/timezone sin comprobar
que sean opciones permitidas.

**Propuesta:** comprobar color `#RRGGBB`, longitudes y URI/host o formato de usuario
por plataforma; validar IDs con CacheService y excluir visibilidades ResourceOnly
para email. Conservar null=sin cambio y vacío=quitar. No borrar valores antiguos
solo porque no vienen en el DTO.

### 5. Likes de comentarios sin unicidad en DB

**Clase/método:** CommentService.AddEditOrDeleteLike y LikeUsers.Build.

**Ubicaciones:** [AddEditOrDeleteLike](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:89),
[mapping LikeUsers](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Models/Builds/LikeUsersBuild.cs:7).

Dos peticiones pueden consultar que no existe voto y añadir dos filas para el
mismo ScoreId/UserId. FirstOrDefault solo modifica/remueve una de ellas, pero Count
cuenta ambas. El botón bloqueado en un navegador no impide peticiones concurrentes
desde otra pestaña/cliente. ScoreUsers ya tiene un índice único; LikeUsers no.

**Propuesta:** añadir un índice único equivalente:

```csharp
entity.HasIndex(like => new { like.ScoreId, like.UserId }).IsUnique();
```

Antes de migrar, eliminar duplicados existentes conservando el voto elegido.
Tratar la carrera de inserción releyendo la fila o devolviendo conflicto, no como
fallo genérico del sistema. No introducir consultas concurrentes en un DbContext.

### 6. Comentarios fuera de un recurso tienen permisos inconsistentes

**Clase/métodos:** CommentService.CheckResourcePermissionFromComment,
AddCommentToComment, GetReplies y GetUserLike.

**Ubicaciones:** [filtro que exige recurso](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:392),
[reply](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:220),
[GetUserLike](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:196).

CheckResourcePermissionFromComment exige ResourceComment != null; por eso votar o
listar replies de un comentario válido sin recurso devuelve CommentNotFound.
AddCommentToComment, en cambio, permite responder si la relación falta y no
comprueba otro contexto. El propietario ya indicó que no todo comentario debe
pertenecer a un recurso.

**Propuesta:** cargar primero el comentario por ID y después validar el contexto
que tenga. Para comentarios de recurso, seguir comprobando acceso al recurso;
para otros contextos, definir la autorización correspondiente. No resolverlo
permitiendo indiscriminadamente todo comentario sin ResourceComment: en datos
antiguos puede ser una respuesta que pertenezca a un hilo privado.

GetUserLike también debería aplicar el permiso del comentario, para que la lectura
del voto y su modificación utilicen la misma regla. Los comentarios nuevos de
recurso sí propagan su ResourceComment a las respuestas; eso está correcto.

### 7. Borrar físicamente un comentario deja su Score y votos

**Clase/método:** CommentService.DeleteComment.

**Ubicación:** [rama sin respuestas](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:370).

La rama borra Comment, pero no su Score. Comment tiene la FK hacia Score: la
cascada funciona al borrar Score, no al borrar Comment. Así quedan Score y
LikeUsers huérfanos. Los Includes Parent/ResourceComment actuales permiten la
cascada cliente de esas relaciones; no son el problema de esta rama.

**Propuesta:** cuando no hay hijos, marcar también el Score para borrar y dejar
que su cascada elimine votos, guardando todo en un SaveChanges. Cuando hay hijos,
mantener el borrado lógico tal como está. Si después se borra la última respuesta
de un padre Deleted, opcionalmente limpiar también ese padre; eso es mantenimiento
adicional y no debe borrar un hilo que aún tenga descendientes.

### 8. La respuesta inmediata de Reply puede perder color/avatar

**Clase/métodos:** CommentService.AddCommentToComment, UserNameDTO.FromUser,
AccountService.GetUser.

**Ubicación:** [construcción del resumen](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:262).

GetUser usa FindAsync y no carga UserInfo/Avatar. FromApplicationUser devuelve
solo ID/nombre si UserInfo no está cargado. En una petición nueva puede llegar una
respuesta publicada con avatar/color ausentes, aunque al recargar GetEnd sí los
proyecta. No es un fallo del renderer: no puede inventar esa información.

**Propuesta:** proyectar un UserNameDTO completo para esa respuesta, o usar una
consulta de resumen público que ya cargue esos datos. No añadir Includes a todos
los usos de GetUser ni hacer una consulta por cada comentario.

### 9. El login web descarta la razón real del fallo

**Clase/método:** LoginController.Login; AccountService.Login.

**Ubicación:** [TryPickT0 que descarta el error](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Controllers/LoginController.cs:21).

DBError, TokenCreationError e InvalidPassword redirigen a result=invalid. La vista
aconseja revisar la contraseña incluso si hay un fallo de DB/configuración.
AccountService también convierte NotAllowed/LockedOut en InvalidPassword.

**Propuesta:** conservar el ServiceResultTypes del fallo para la redirección y
mostrar un mensaje de indisponibilidad cuando corresponda. Para lockout/email sin
confirmar, usar códigos específicos según lo que quieras revelar. Antes de cambiar
la query de la redirección, acordar el contrato para conectarlo en Login. No devolver
exception.Message ni stack traces al usuario.

### 10. Tipos duplicados y límite de autores del Create

**Clase/métodos:** ResourceService.GetTypes y Create.

**Ubicaciones:** [GetTypes](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:900),
[límite de creación](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:680).

GetTypes no deduplica IDs y Create no aplica la deduplicación final que sí hace
Edit. Un DTO con IDs repetidos puede guardar relaciones repetidas. El límite de
50 autores se comprueba antes de que GetAuthors añada al submitter por IAmAuthor;
50 autores externos más IAmAuthor puede dejar 51 créditos.

**Propuesta:** iterar typeIds.Distinct() y comprobar el total real después de
obtener autores internos/externos, antes del guardado. Validar VideogameId y
VisibilityId antes de escribir archivos para devolver errores de input y no de
FK. Validar nombres/URLs de dependencias externas y longitud de tags/autores según
sus mappings. Son controles pendientes anteriores, no se afirma que se hayan
introducido en esta refactorización.

### 11. El trabajo de inspección no se limita con el peso comprimido

**Clase/método:** FileService.InspectImage.

**Ubicación:** [límites/decodificación](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/FileService.cs:405).

Los límites independientes permiten aproximadamente 4,2 millones de píxeles y
1.800 frames; el bucle decodifica todos. Eso permite procesar hasta unos 7.500
millones de píxeles de salida para una animación de poco peso. La validación de
contenido está implementada, pero un límite de 5 MB no limita su costo de CPU.

**Propuesta:** establecer un presupuesto combinado de píxeles×frames y límites
por contexto, especialmente para avatar, antes del bucle. Decidir el presupuesto
con ejemplos de medios legítimos; no se midió aquí un archivo malicioso ni se
afirma un tiempo concreto de ejecución. Evitar sustituir la inspección real por
confiar en el MIME del cliente.

### 12. Notificaciones Discord prolongan la respuesta después del commit

**Clase/métodos:** ResourceService.Create, NewsService.CreateInternal,
AccountService.ConfirmUser y EditUserInfo.

**Ubicación de ejemplo:** [Create](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:800).

El commit ya terminó y se espera NotifyBotAsync antes de devolver el éxito. El
cliente DiscordBot tiene timeout de 10 segundos. La notificación puede añadir
esa espera a una operación que ya está guardada, aunque su resultado se ignore.

**Propuesta:** si se necesita reducir esa latencia, publicar un evento en una cola
procesada por un worker/outbox. No usar Task.Run con el servicio/DbContext de la
petición. Mientras se mantenga await, preservar el éxito del guardado aunque falle
Discord, como ahora hace el código. La refactorización no convierte esa llamada
en una transacción conjunta con el bot.

### 13. Consultas de permisos de comentarios siguen cargando colecciones

**Clase/métodos:** CommentService.CheckResourcePermission y ResourceService.AddComment.

**Ubicaciones:** [CheckResourcePermission](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/CommentService.cs:420),
[AddComment](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:125).

Incluyen todas las versiones/autores para comprobar coincidencias. En GetReplies
y paginación esto se repite antes de leer cada bloque. La proyección de CommentReadDto
ya evita traer todos los votos, pero no elimina esta carga previa.

**Propuesta:** proyectar VisibilityId y un bool de acceso con Any, como GetAccess
en ResourceService. No quitar Includes que sí necesita una operación de edición
después de materializar. AddVersion/EditVersion también pueden reducirse a acceso,
versión y carpeta necesaria; no es una razón para volver a un GET gigante.

## P3 — Prioridad baja

### 14. Se intercambiaron archivo y método al construir el error

**Clase/métodos:** DBHelperService.CreateFailure y RunErrorCallback.

**Ubicaciones:** [línea 101](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/DBHelperService.cs:101),
[línea 115](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/DBHelperService.cs:115).

Return recibe filePath y luego methodName, pero esas dos llamadas los pasan al
revés. Los logs/contextos señalan un nombre de método como archivo y una ruta
como método. No rompe el guardado, pero dificulta diagnosticar los fallos.

**Corrección propuesta para ambas:**

```csharp
return result.Return(_logger, exception,
    filePath: filePath, methodName: methodName, line: line);
```

En RunErrorCallback, usar ServiceResult.Error y conservar que no se devuelve ese
resultado. Los parámetros nombrados evitan volver a intercambiarlos.

### 15. El mapper no contempla NotImplemented

**Clase/método:** ServiceResultExtensions.GetActionResult.

**Ubicación:** [switch](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/DTOs/ServiceResultExtensions.cs:17).

UnsupportedEvent tiene ActionResultType.NotImplemented, pero cae en el default
500. Es un hueco del mapper central; no se afirma que una página actual ya esté
recibiendo este resultado.

**Corrección propuesta:**

```csharp
ActionResultType.NotImplemented =>
    controller.StatusCode(StatusCodes.Status501NotImplemented, response),
```

### 16. Autores internos en orden Z–A

**Clase/método:** ResourceService.GetResourceAllAuthors.

**Ubicación:** [ordenación](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorage/Services/ResourceService.cs:1152).

La query usa OrderByDescending(UserSummary.Username), aunque se pidió orden
alfabético ascendente. Dependencias/autores externos también usan descending;
decidir si se desea A–Z para ellos.

**Propuesta:** OrderBy y mantener ThenBy(Id). No cambia el contrato.

## Proyecto de tests: compilación pendiente, no ejecutados

La solución compilada con `-m:1` reportó:

| Archivo/línea | Problema | Qué adaptar |
| --- | --- | --- |
| [RegisterControllerTests.cs:29](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorageTesting/RegisterControllerTests.cs:29) | Pasa SnestorageContext donde ahora se recibe CacheService | Preparar CacheService y su scope con el contexto de prueba; no pasar null porque GetRegistrationOptions lo usa |
| [ProfileSocialLinksTests.cs:133](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorageTesting/ProfileSocialLinksTests.cs:133) | Usa SocialFields en una fixture del Profile anterior | Adaptar al componente UserProfileView actual; revisar también la reflexión del campo _profile que ya no pertenece a esa página |
| [EmailConfirmationTests.cs:128](D:/Users/pndia/Documents/GitHub/SNEStorage/SNEStorageTesting/EmailConfirmationTests.cs:128) | Llama Dispose que fue reemplazado por DisposeAsync | Usar el lifecycle asíncrono y actualizar la fixture al transporte JS actual, no solamente renombrar esa llamada |

La compilación de tests no demuestra que estos sean sus únicos fallos de ejecución.
No se editaron ni se ejecutaron tests como parte de esta revisión.

## Decisiones anteriores que siguen requiriendo definición

- Reglas definitivas de Seal of Quality: Edit/EditVersion no imponen por sí solos
  el permiso del staff para modificar un recurso/versiones con sello.
- Dependencias privadas/cambios de visibilidad/ciclos: GetDependencies valida
  existencia, no toda esa normativa. Se había acordado tratar el diseño después.
- Bans: Discord comprueba la tabla Ban, pero Login/ValidateJwt no comprueban esas
  filas. Si Ban debe impedir acceso web, coordinarlo con login y revocación; no
  confundir el lockout de Identity por contraseñas incorrectas con esa tabla.
- Rollbacks de archivo fuera del fallo de SaveChanges siguen pasando a veces el
  mismo ct cancelado y pueden conservar archivos/manifiestos. Se mantiene la
  decisión del propietario de limpieza periódica; no se presenta como un rollback
  garantizado por SQL ni se añadió otro try grande.
- Descargar con rangos/reintentos puede contar más de una petición. El incremento
  actual es atómico y sí está implementado; definir qué cuenta como descarga si se
  requiere una métrica diferente.

## Cosas comprobadas como correctas en el código leído

- Los controladores extraen la alternativa de OneOf y no serializan el contenedor
  completo en errores. ServiceResultContext no se expone con ruta/exception al cliente.
- Password reset sigue usando Identity, tokens codificados y acceso anónimo;
  la solicitud no revela inexistencia de cuenta mediante UserNotFound.
- Registro usa transacción para Identity+UserInfo+File y no concede JWT antes de
  confirmar el email. El error de envío posterior no revierte la cuenta guardada.
- Los resultados de guardado/error del batch se propagan; el callback de fallo de
  SaveChanges limpia con CancellationToken.None.
- GetFile comprueba pertenencia del archivo al recurso; Download conserva control
  de acceso e incremento SQL atómico.
- Get principal sigue reducido; secciones, previews, autores y rating tienen
  acciones separadas. SelectFind/Get mantienen AsSplitQuery y DTOs.
- CacheService no comparte DbContext singleton y protege las cargas con semaphore;
  los loaders crean scopes independientes. VideogameService no carga ni cachea el
  catálogo entero para el picker.
- CaptchaFilter devuelve después de next y distingue Valid de Invalid/Unavailable;
  las políticas y rutas frontend siguen concordando. Rate limiting/antiforgery
  siguen presentes.
- Los cambios observados en las dos migraciones listadas fueron eliminación de
  un using, no cambios de esquema.

Estas comprobaciones son de código, no una prueba integral de producción. Para
continuar, conviene corregir primero el punto 1, después la distinción de errores
de autenticación y los permisos de consultas independientes.
