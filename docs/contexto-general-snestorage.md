# SNEStorage: contexto para continuar el proyecto

**Última actualización: 15 de octubre de 2026.**

Este documento resume el producto, los acuerdos de trabajo y el estado del proyecto. Está pensado para adjuntarlo a otra conversación. La información combina decisiones del propietario, trabajo realizado en conversaciones anteriores y lectura del código actual; no constituye una auditoría completa ni una comprobación del despliegue.

Si el código cambia después de esta fecha, comprobar los contratos actuales antes de modificar el frontend. Los documentos de propuestas anteriores pueden contener problemas que ya se solucionaron.

## 1. Instrucciones esenciales para quien continúe

- **El propietario desarrolla el backend. El asistente no modifica directamente Services, Controllers, Models, DTOs, migraciones, Program.cs ni el csproj sin autorización expresa para esa tarea.**
- El asistente desarrolla el frontend: componentes Razor, sus archivos `.razor.cs` y `.razor.css`, JavaScript, CSS, contenido público JSON y documentación.
- Puede leer el backend para revisar contratos, permisos y resultados. Si necesita un cambio, debe explicar: **«Oye, necesito que edites esto en el backend para que funcione»**, indicando el archivo, método y cambio concreto. Puede entregar código o una propuesta en un documento para que el propietario lo aplique.
- Una petición de «revisar y arreglar el frontend» no autoriza a corregir silenciosamente el backend.
- Comunicarse en español. La interfaz y el contenido público del sitio están principalmente en inglés.
- Seguir el estilo existente: código sencillo, operadores ternarios cuando encajan, pocos helpers y separación entre markup, lógica y estilos. El propietario rechaza refactorizaciones con muchos métodos auxiliares o grandes bloques try/catch que envuelvan todo.
- Preferir trabajar los problemas uno por uno y mantener el tema acordado. No sustituir el siguiente punto por otro asunto ajeno.
- No introducir cambios de diseño que deshagan mejoras ya aprobadas, especialmente la disposición de la vista de recursos y el brillo del sello.
- No ejecutar ni añadir tests salvo que se solicite. No afirmar que algo se probó en navegador o producción si solo se inspeccionó o compiló.
- Preservar los cambios del propietario y de otras conversaciones. Puede haber trabajo concurrente, especialmente en la integración y el bot de Discord.
- No publicar, migrar producción, borrar registros/archivos ni hacer operaciones de Git destructivas como parte de una revisión. Los secretos no deben aparecer en documentos, logs enviados al cliente ni mensajes.

## 2. Qué es la aplicación

SNEStorage es un archivo comunitario de recursos relacionados con **Super Nintendo / SNES**: herramientas, documentación, código, gráficos, música, homebrew y recursos para hacking y desarrollo.

El creador es **anonimzwx**. Su perfil en el sitio usa el ID público `79b39789-949b-4e0a-9ef9-78eabaf4c3c5`. About debe enlazar a ese perfil y usar sus datos públicos, color y avatar. Sus redes personales no deben ocupar el footer general.

`/Credits`, en Info después de About, acredita al creador con su perfil público y a los proyectos externos. Se edita desde `wwwroot/Content/credits.json`. Flips/Alcaro figura como referencia del aplicador independiente en JavaScript; Telinc1 y snes_spc/Shay Green figuran como código adaptado para audio. Incluye enlaces a proyectos, licencia y fuente local del reproductor.

El sitio ya se publicó en **https://snestorage.dev**, alojado en **Monster.ASP**, con SQL Server. El propietario indicó que los servidores están en Estados Unidos. Esto no establece su país de residencia ni el domicilio legal del proyecto.

Correo público de soporte/envío: **support@snestorage.dev**. El correo personal usado al inicio ya no debe proponerse como contacto público.

## 3. Arquitectura y mapa del repositorio

Raíz del workspace actual: `D:\Users\pndia\Documents\GitHub\SNEStorage`.

Las rutas de esta tabla son relativas a esa raíz:

| Ubicación | Responsabilidad |
| --- | --- |
| `SNEStorage/` | Aplicación web ASP.NET Core, .NET 10 |
| `SNEStorage/Components/Pages/` | Páginas Blazor |
| `SNEStorage/Components/Shared/` | Componentes reutilizables y acceso a páginas |
| `SNEStorage/Components/Resources/` | Formularios, pickers, versiones y comentarios de recursos |
| `SNEStorage/Components/News/` | Noticias, tarjetas, formularios y carga de listas |
| `SNEStorage/Components/Layout/` | Header, cuenta, menú lateral y footer |
| `SNEStorage/wwwroot/js/` | Fetch desde el navegador e interacción con formularios |
| `SNEStorage/wwwroot/Content/` | Documentos públicos JSON editables |
| `SNEStorage/Controllers/` | Endpoints HTTP; responsabilidad del propietario |
| `SNEStorage/Services/` | Lógica de negocio, archivos, email, caché y consultas |
| `SNEStorage/Services/Interfaces/` | Interfaces como IFullGet, ICreate e IEditSingle |
| `SNEStorage/Models/` y `Models/Builds/` | Entidades y mappings de EF Core |
| `SNEStorage/DTOs/` | Contratos de entrada y lectura |
| `SNEStorage/Migrations/` | Evolución del esquema SQL Server |
| `SNEStorage/EmailTemplates/` | Plantillas de confirmación y recuperación de contraseña |
| `SNEStorage.DiscordBot/` | Proyecto separado para el bot y la comunidad Discord |
| `discord/server-config.json` | Configuración del servidor Discord |
| `SNEStorageTesting/` | Proyecto de tests existente |
| `docs/` | Propuestas, diagnósticos y documentación |

Tecnología principal: Blazor **Interactive Server**, ASP.NET Core MVC/API, EF Core SQL Server, Identity con ApplicationUser, JWT y resultados `OneOf`. Desde la refactorización del 13 de octubre los servicios usan **ServiceResultTypes, ServiceResult y ServiceResultContext** como resultado común, con ServiceResultExtensions para mapear respuestas HTTP. Los enums anteriores por servicio se eliminaron y EmailController/FileController/GetLikeController ya no existen. El frontend usa módulos JavaScript y peticiones con cookies del navegador; el backend es la autoridad para permisos y validaciones.

Dependencias relevantes: MailKit para correo, SkiaSharp para imágenes, SharpCompress para archivos comprimidos, MediaInfo.Wrapper.Core para inspección de video, y Swagger. Ver versiones actuales en el csproj; no actualizar paquetes sin una razón concreta.

## 4. Páginas y navegación

| Ruta de frontend | Uso |
| --- | --- |
| `/` | Landing con portada, noticias a la izquierda y recursos a la derecha |
| `/Resources` | Buscador/lista de recursos; también existen aliases `/Resource` y `/Resource/Index` |
| `/Resource/{id}` | Vista individual |
| `/Resource/Create` | Creación; `/Resources/Create` es un alias |
| `/Resource/Created/{id}` | Mensaje de éxito y redirección al recurso después de unos segundos |
| `/Resource/{id}/Edit` | Edición de información del recurso |
| `/Resource/{id}/Versions/Create` | Nueva versión |
| `/Resource/{id}/Versions/{versionId}/Edit` | Edición de versión |
| `/News` y `/News/Create` | Lista de noticias y creación |
| `/News/{id}` | Noticia completa, pública; edición para mods/admins |
| `/Tools/ApplyPatches` | Aplicador público de BPS/IPS, completamente local en el navegador |
| `/Tools/SPCPlayer` | Biblioteca SPC en IndexedDB y reproductor flotante que continúa durante la navegación dentro de la app |
| `/Login` y `/Register` | Login y registro |
| `/Register/CheckEmail` | Instrucciones para confirmar el correo |
| `/Register/Confirmation` | Confirmación; admite variante con UserId y usa query `token` |
| `/Profile/me` | Perfil propio; `/Profile` sigue siendo alias |
| `/Profile/{userId}` | Perfil público ajeno, identificado por ID, no username |
| `/Profile/Edit` | Editor propio |
| `/Account/ForgotPassword` | Solicitar email de recuperación; alias `/Profile/Password` |
| `/Account/ResetPassword` | Establecer contraseña mediante enlace/token |
| `/Rules`, `/SealOfQuality`, `/Privacy` | Información pública |
| `/Community`, `/About`, `/Donations`, `/Credits` | Información pública |
| `/Users` | Búsqueda pública por username con avatar y color; páginas de 20/50/100/200/All con carga por scroll en bloques de 20 |
| `/Error`, `/Error/{status}`, `/not-found` | Errores |

El header tiene el logo arriba a la izquierda y controles de cuenta a la derecha. El menú principal está en una barra **lateral derecha**, empieza debajo del header y se puede minimizar. En móvil es un drawer. No volver a sobrecargar la barra superior con navegación ni añadir un botón Home al menú lateral.

Info, Community y Tools empiezan colapsados. Info conserva News visible; Community conserva Community y Donations visibles y oculta Users; Tools oculta todos sus enlaces (Apply IPS/BPS y SPC Player).

La cuenta muestra avatar con margen, username en su color y un menú con perfil/logout. Deslogueado se muestran login/registro. Durante la comprobación inicial se reserva espacio con un placeholder neutral; no se debe mostrar brevemente una identidad antigua.

### Aplicador de parches

`/Tools/ApplyPatches` aplica BPS/IPS a ROMs `.sfc`/`.smc` usando JavaScript y un Web Worker. Los archivos se leen únicamente en el navegador, sin `InputFile`, endpoints de subida ni persistencia en DB. Es un motor compatible independiente; no ejecuta `flips.exe`. Valida estructura SNES, límites, estructura del parche y los CRC que BPS proporciona. IPS no permite verificar el juego/revisión base. Ver `docs/rom-patcher-2026-10-15.md` para límites, cabeceras y comprobaciones pendientes.

### Biblioteca SPC local

`/Tools/SPCPlayer` guarda archivos y preferencias en IndexedDB del navegador, sin subirlos al servidor. Admite selección múltiple/drop, orden, eliminación, deduplicación, volumen, shuffle y repeat. Cada canción tiene ajustes de duración y número de reproducciones/bucles. Lee tiempos ID666/xid6; sin duración conocida sigue hasta Next/Stop por defecto, con duración predeterminada opcional. El reproductor flotante vive en MainLayout fuera de PageAccessGuard; no se detiene al desmontar la página de la biblioteca. Incluye modo mínimo y cursor para saltos de un segundo con avance cooperativo del emulador. Comparte el motor SPC con las previews y coordina un solo propietario de audio. No se garantiza continuidad al recargar completamente, cerrar la pestaña o suspender el navegador. Conservar los originales porque borrar los datos del sitio borra la biblioteca. Ver `docs/spc-playlist-2026-10-15.md`.

## 5. Recursos y versiones

`Resource` contiene datos generales, videojuego, visibilidad, flags sensibles y relaciones. `ResourceVersion` contiene archivo, etiqueta Version, ChangeLog, descargas, submitter, fechas y los campos **AlreadyModerated, SealOfQualityRequested y SealOfQuality** de esa versión. Resource también tiene AlreadyModerated y fechas propias.

Autores internos son usuarios de SNEStorage, con resumen público; autores externos son nombres sin cuenta. Hay tags, tipos agrupados por categoría, previews, dependencias internas y externas. **Teams se dejó fuera del alcance inicial**.

Los formularios se dividen en tabs: información general, archivo/notas cuando corresponda, previews, autoría, dependencias y contenido sensible. En el editor general no se cambia una versión ni se debe añadir SealOfQualityRequested a ResourceEditDTO: esa petición se hace al crear o editar versiones.

### Reglas de UX ya acordadas

- Videojuego: combobox con búsqueda remota, debounce y resultados limitados; Any (ID 1) y Other (ID 2) primero cuando no se busca algo específico. No cargar el catálogo completo, de unos 1.700 videojuegos, como opciones iniciales.
- Tipos: picker compacto por categorías colapsadas. Títulos de categoría con espacio para nombres largos; indicador pequeño **«X selected»**, sin X/Y. Tipos que contengan «Other» van al final de su propia categoría.
- Categorías del filtro se reciben de las opciones/caché de tablas, no de una lista fija ni solo de categorías que ya tengan tipos.
- Autores: selección múltiple sin repetidos, búsqueda de usuarios; externos permiten entrada libre y sugerencias. Checkbox **I am the author** separado de los IDs seleccionados.
- Tags: entrada con comas; mantener selección/deduplicación coherente con el DTO.
- Archivo principal: ZIP, RAR, 7Z, TAR, GZ/TAR.GZ; máximo 250 MiB. Hasta 20 previews de imagen/GIF/video, máximo 5 MiB por archivo. Los límites HTTP/proxy deben permitir el total y el overhead.
- Etiquetas de versión: `X`, `X.X`, `X.X.X`, opcional `-alpha`, `-beta` o `-rc`, y número opcional, como `1-alpha.10` o `2.50-beta`. Controles separados construyen el string. Actualmente se limita a 25 caracteres y componentes numéricos de 1–4 dígitos. Backend valida formato y orden; una versión antigua editada debe conservar el orden respecto de sus vecinas.
- Eliminar relaciones existentes usa los IDs de la relación correspondiente: por ejemplo, una preview se elimina por su ID de preview, no por FileId. Confirmar cada DTO antes de construir el payload.
- No hacer Distinct por ID 0 entre entidades nuevas: todas comparten ese valor y se perderían elementos.

### Vista individual y rendimiento

El GET principal se redujo a **ResourceReadDTO**: datos generales, flags, CanEdit, videojuego, visibilidad, tipos/categorías y tags. Las secciones restantes se obtienen mediante acciones separadas; mostrar un loader en la sección mientras carga y tratar su error sin inutilizar todo el recurso.

No restaurar un GET gigante con todas las relaciones. Las consultas con seguimiento para editar pueden necesitar relaciones que el GET público ya no incluye.

Disposición aprobada: título y estrellas, autores, clasificación/descripción, avisos, dependencias, descarga de la última versión junto a la zona de descripción y previews **centradas**. Imágenes/GIFs compactos alrededor de 256×224; videos pueden ampliarse. Tab de historial de versiones y controles de edición solo con sesión verificada y permisos.

Las fechas de la lista deben ocupar poco espacio junto a tags. **Last Version Created At no equivale a Updated At.** Botón de descarga primero y contador pequeño debajo. Mostrar cinco tags y desplegar el resto. Autores/tipos/categorías con expansión si son muchos.

Lista: orden predeterminado reciente a antiguo, filtros agrupados y colapsables, nombre arriba. Cantidades de página 20/50/100/200/all; para 100 o más, carga del navegador por bloques de 50 al hacer scroll. No solicitar todos los registros en una única query porque se eligió all.

## 6. Contratos API importantes

Estos son contratos observados en el código al crear el documento. Revisar las firmas y DTOs si el propietario vuelve a cambiarlos. Una ruta de página no es la ruta API.

| Método/ruta | Función |
| --- | --- |
| `GET /api/resource/find` | Filtros ResourceFinderDTO por query, maxAmount y page |
| `GET /api/resource/options` | Tipos con categoría, visibilidades y categorías |
| `GET /api/resource/{id}` | ResourceReadDTO |
| `GET /api/resource/{id}/authors` | Autores internos |
| `GET /api/resource/{id}/external-authors` | Autores externos |
| `GET /api/resource/{id}/dependencies` | Dependencias internas |
| `GET /api/resource/{id}/external-dependencies` | Dependencias externas |
| `GET /api/resource/{id}/versions` | Historial |
| `GET /api/resource/{id}/versions/latest` | Última versión |
| `GET /api/resource/{id}/rating` | Puntuación |
| `GET /api/resource/{id}/previews` | Previews |
| `GET /api/resource/{id}/file/{resourceFileId}` | Metadatos JSON; no asumir que retorna bytes |
| `GET /api/resource/{id}/file/{resourceFileId}/download` | Archivo como descarga; incrementa el contador |
| `POST /api/resource/create/submit` | ResourceCreateDTO, multipart |
| `PUT /api/resource/{id}/edit` | ResourceEditDTO, multipart |
| `POST /api/resource/{id}/version/create/submit` | Nueva versión, multipart |
| `PUT /api/resource/{id}/version/{versionId}/edit` | ResourceVersionEditDTO, multipart |
| `GET /api/videogames/search` | Búsqueda por likeString y maxAmount |
| `GET /api/user/search` | Autores usuarios, resúmenes públicos |
| `GET /api/externa-author` | Sugerencias externas; conservar esta grafía actual |
| `GET /api/resource/search` | Búsqueda de dependencias |
| `GET /api/profile/me` | UserInfo privado de la cuenta actual |
| `GET /api/profile/{userId}` | Perfil público, AllowAnonymous |
| `PUT /api/profile/me/edit` | EditProfileDTO, multipart |
| `POST /api/login/submit` | Login web, cookie y redirección |
| `POST /api/login/logout` | Logout web |
| `POST /api/register/submit` | Registro web |
| `POST /api/register/confirmed/{userId}?token=...` | Confirmación |
| `POST /api/account/password/request` | Solicitud por email, JSON y acceso anónimo |
| `POST /api/account/password/change` | Cambio con token, JSON y acceso anónimo |
| `POST /api/account/confirmation/resend` | Reenvío de confirmación |
| `GET /api/news` | Noticias paginadas y ordenadas |
| `POST /api/news/create` | Crear noticia |
| `PUT /api/news/{newsId}/edit` | Editar noticia |

Los resultados de negocio usan **OneOf<T, ServiceResultContext>**, o ServiceResultContext para acciones sin datos. El estándar de errores serializados es **`NombreController-ServiceResultTypes`**, por ejemplo `ResourceController-InvalidFile`. El prefijo identifica el controlador, no el servicio que produjo el fallo: un error de archivo puede venir desde ResourceController, RegisterController o ProfileController. El frontend ya trata esos errores compartidos y respuestas 204; conserva compatibilidad con algunos contratos antiguos. No serializar el OneOf ni el contexto de excepción directamente.

Autorización fallida suele devolver 401/403 según el caso; falta de datos, 404; input inválido, 400; DBError, 500; caché/servicio no disponible, 503; rate limiting, 429. Una cancelación puede mapearse a 499. No interpretar un DBError/timeout como «usuario deslogueado».

## 7. Comentarios, votos y noticias

Comentarios se leen como **CommentReadDto**, con agregados Likes/Dislikes, MyVote, HasReplies y resumen público. No reconstruir esos datos descargando todas las filas de votos.

- `GET /api/comment/resource/{resourceId}`: comentarios raíz paginados/ordenados.
- `GET /api/comment/{commentId}/replies`: respuestas.
- `GET /api/comment/{commentId}/userlike`: voto propio.
- `POST /api/comment/{commentId}/like`: like/dislike/remove.
- `POST /api/comment/{commentId}/reply`, `PUT /api/comment/{commentId}`, `DELETE /api/comment/{commentId}`.
- Publicar raíz: `POST /api/resource/{id}/Comment/Submit`.
- Puntuar recurso: `POST /api/resource/{id}/Score/Submit/{value}`.

Los botones de like/dislike muestran el estado confirmado. Pulsar el mismo voto lo quita. No mostrar controles para votar comentarios propios, borrados o sin sesión. No permitir responder comentarios borrados. Un comentario borrado sin respuestas se oculta; si conserva respuestas, mostrar un placeholder.

Mostrar username/color, avatar, publicación y edición. Reply abre la caja y al publicar la cierra; desplazar hacia el nuevo comentario. Las listas y respuestas cargan más al hacer scroll, con paginación debajo. El servicio admite comentarios fuera de recursos: no asumir que cualquier comentario pertenece a uno.

Noticias tienen título y cuerpo, orden por antigüedad, lista con carga progresiva y edición. Crear/editar noticias debe ser accesible solo a mods/admins; ocultar botones para visitantes y usuarios sin ese permiso.

Las tarjetas de la lista enlazan por el título a `/News/{id}` y muestran un extracto
de hasta 6 líneas visibles (4 en el landing), ajustado por CSS al ancho disponible.
Los textos cortos se muestran completos, sin reservar altura vacía. El extracto usa
texto de Markdown saneado y conserva saltos de línea; no se recorta por caracteres.
La vista individual usa `GET /api/news/{id}`, muestra Markdown completo, autor con
avatar/color y fecha, y permite editar a staff con sesión verificada. Crear una noticia
redirige a esa vista. Los likes/dislikes ya están conectados en landing, lista e individual mediante NewsReactions, con captcha condicional y sesión verificada. GET devuelve Likes, Dislikes y MyVote; pulsar el voto activo envía Remove. Los comentarios de noticias siguen pendientes.

## 8. Perfiles y Discord

`ApplicationUser` hereda de IdentityUser. Las navegaciones hacia ApplicationUser se han ocultado en varias respuestas con **JsonIgnore** para evitar exponer datos de Identity; siguen funcionando en EF/backend. El frontend consume **UserNameDTO/UserSummary**, no depende de `User.UserName` en JSON.

Fecha de nacimiento: visible únicamente en el perfil propio. ProfileController devuelve `DateOnly.MinValue` en perfiles ajenos **intencionalmente**; no corregir ese valor ni mostrárselo a visitantes. Email debe respetar su visibilidad. UsernameColor se usa para pintar nombres, no como información técnica que haya que mostrar en el perfil.

Username, email de cuenta y cumpleaños no se editan desde el formulario propio: requieren staff. Para campos editables, **null significa sin cambios**; **string vacío significa quitar el valor**, especialmente redes sociales. Un string vacío no son dos comillas literales. Conservar esa diferencia en JSON/multipart y en model binding.

Discord está separado y antes que las otras redes en Edit profile, porque tiene integración. Es una cuenta/usuario, no un servidor personal que se introduce como enlace social.

### Comunidad

- Servidor principal SNEStorage: **https://discord.gg/e8bHBcRKX9**, logo SNEStorage.
- Servidor secundario/personal SNES Office: **https://discord.gg/NpUXn7H**, imagen local en `wwwroot/images/community/`.
- `/Community` y sus guías son editables en `wwwroot/Content/community.json`.
- Las guías empiezan con el requisito de tener una cuenta registrada con email confirmado; los pasos parten de que ya existe esa cuenta, sin mezclar instrucciones de registro.
- Flujo indicado por el propietario para la guía desde el sitio: perfil propio → **Connect to Discord** (vincula la cuenta actualmente conectada en Discord y redirige al servidor) → entrar al servidor por redirección o invitación directa → reglas → **Allow Access**. Cambiar cuenta: Edit profile → **Change Discord account**. No añadir pasos de autorización manual a esta guía.
- Guía desde Discord: solo tres pasos, iniciar sesión en SNEStorage → entrar al servidor → leer reglas y pulsar **Allow Access**. No añadir más pasos del bot a las instrucciones.
- OAuth empieza en `/api/discord/link/start` y regresa a `/api/discord/link/callback`. Si falta login, se usa returnUrl para reanudar.
- El bot consulta estado mediante `/api/internal/discord/member-state/{discordUserId}` y sincroniza acceso, nombre, color y roles pertinentes. Hay configuración/credenciales entre app y bot: no convertir el endpoint interno en una API pública sin protección.

## 9. Login, correo y persistencia

Flujo de registro: crear cuenta → enviar confirmación → CheckEmail → confirmar con token → login. `RequireConfirmedEmail = true` está configurado. Confirmación y reset usan los proveedores de tokens de Identity; no diseñar validación casera basada solo en que el string sea Base64.

La web usa cookie JWT **`__Host-SNEStorage.Auth`**, HttpOnly, Secure, SameSite=Lax, Path=/ y vencimiento. Un middleware la copia a Authorization antes de autenticar. Leer el ID con `ClaimTypes.NameIdentifier`, no con la cadena corta `"nameidentifier"`.

Estado actual observado:

- AccountService genera JWT por 90 días con `security_stamp`.
- Login de Identity usa `isPersistent: true`; cookie Identity por 90 días y SlidingExpiration.
- OnTokenValidated verifica usuario/security stamp. JWT de navegador con menos de 45 días restantes puede renovarse y reescribir la cookie.
- Esto es renovación de un JWT aún válido, **no un sistema separado de refresh tokens rotatorios**. El frontend no necesita llamar periódicamente al antiguo endpoint refresh-token.
- JWTKey debe mantenerse estable al publicar. Un cambio invalida tokens.
- Data Protection está configurado con PersistKeysToFileSystem y ApplicationName SNEStorage. `DataProtection:KeyPath` es obligatorio y debe apuntar a una carpeta persistente/escribible fuera de publicación y de los archivos públicos. Se creó una carpeta DataProtection-Keys en Home de Monster.ASP; comprobar la ruta física real/configuración del entorno.
- Reiniciar todavía pierde Session en memoria; la persistencia del login web depende de las cookies/keys, no de guardar el JWT en Session. OAuth en progreso puede depender de Session.

El propietario quiere evitar relogueos frecuentes, incluso tras reinicios o deploys. No prometer login eterno: expiración, revocación, borrado de cookies o cambio de claves pueden requerir login.

`account-session.js` comparte una consulta pendiente a `/api/profile/me`, con snapshot en memoria del navegador durante 15 segundos y deadline de 30 segundos. Header/guard/recurso/CheckEmail reutilizan la petición. Un consumidor desmontado no debe abortar la petición de los demás. No guardar datos de perfil en localStorage ni cachearlos en el servidor.

Login debe volver a la página anterior mediante returnUrl local seguro. Logout usa POST y navegación completa para crear un circuito nuevo. No simular logout con un booleano del frontend.

## 10. Archivos, DB y rendimiento

Guardar archivos y guardar DB son operaciones distintas: una transacción SQL no revierte archivos del disco. FileService prepara el modelo File sin guardar inmediatamente la entidad, registra rutas en un batch/manifiesto y ofrece BatchComplete/BatchRollback. Se ha optado por mantenimiento periódico para huérfanos/manifiestos cuyo cleanup falle; no reintroducir una lista estática compartida de uploads de usuarios.

La URL persistida de File es relativa a **ContentRoot de la aplicación**, no necesariamente a wwwroot. Puede empezar por `wwwroot/`; el frontend debe convertir únicamente rutas públicas apropiadas en URL web. Archivos de recursos en Storage no se pueden abrir directamente como estáticos públicos. GET de metadatos y endpoint de download tienen comportamientos distintos.

FileService tiene validación de contexto (Avatar/Previews/Resource), MIME/alias, inspección de imagen/video/comprimidos y límites. Inspeccionar el código antes de repetir propuestas antiguas: ya se implementaron muchas. MIME y extensión enviados por el cliente no sustituyen la validación real; streams/decodificadores pueden fallar aunque el stream inicial parezca válido.

El propietario decidió conservar la cascada del avatar en su mapping. Ya hubo un saneamiento manual SQL que eliminó un avatar y afectó UserInfo: **no borrar avatares a mano ni cambiar cascadas sin revisar el grafo y discutirlo**.

DBHelperService centraliza operaciones con **OneOf<T, ServiceResultContext>** o ServiceResultContext y registra excepciones con ILogger. TrySyncContext limpia todo el ChangeTracker al fallar; eso fue deliberado. Los callbacks de error se esperan y sus fallos también se registran. Las cancelaciones se distinguen según ct. `T?` genérico sin constraint no garantiza null para un int: default(int) es 0; el resultado actual OneOf distingue datos y error.

Rendimiento:

- Proyectar DTOs y agregados en SQL; no traer todos los votos para Average/Count.
- Mantener IQueryable hasta filtrar/ordenar/paginar y materializar. Navegaciones en Where/Select se traducen a SQL y no necesitan Include para ese uso.
- Include solo para relaciones necesarias después de materializar. AsSplitQuery reduce explosión de colecciones, pero no elimina datos sobrantes.
- Evitar N+1: autores/tags/tipos por lotes. No ejecutar consultas paralelas sobre el mismo DbContext.
- Orden estable con desempate por ID; validar page/maxAmount y calcular offset como `(long)maxAmount * page` antes de convertirlo.
- Métodos C# arbitrarios dentro de ExecuteUpdate/Where pueden no traducirse. Para null/sin cambio y vacío/quitar usar una expresión traducible o calcular parámetros fuera de la query.
- SQL Server no compara columnas text en DISTINCT/orden/filtros habituales; usar tipos nvarchar apropiados y una migración que conserve datos.

**Caché autorizada:** el propietario inicialmente rechazó cachear perfiles/negocio en backend, pero luego aprobó tablas estáticas. CacheService singleton usa IMemoryCache, FrozenDictionary, carga protegida por SemaphoreSlim y scopes propios: FileType (ID/MIME), Visibility, ResourceCategory, ResourceType y TimeZone. Vida actual 24 horas. Devuelve copias de diccionario para lectura. Videogames no se cachea como catálogo completo.

## 11. Diseño y cambios recientes que conservar

- Estética oscura/violeta, superficies semitransparentes para ver el fondo. **Header opaco**. Checkboxes/selects/textboxes con contraste y sin blancos ajenos al tema.
- Flechas/iconos reales, no letras v/^ como controles. ActionArrow para botones. Required fields antes de submit y alineado a la izquierda.
- Registro centrado, sin panel explicativo a la izquierda: avatar circular clicable junto a username, email debajo de username, luego passwords, cumpleaños y preferencias. Color a la izquierda y selectores a la derecha.
- AvatarPicker reutilizado en registro/editor: crop circular, desplazar, zoom y reducir imagen; genera PNG. Avatares mostrados con aire dentro del marco.
- BoxLoader usa **la caja y colores originales separados del diseñador**, no una caja reconstruida. Colores azul → rojo → amarillo → verde con desvanecimiento; ciclo rápido de 0,4 s. No quedan delays artificiales de carga. EmptyBoxIllustration usa caja con gota de sudor encima/integrada.
- Seal of Quality: logo y título dorado con brillo diagonal de arriba izquierda a abajo derecha, rápido y sincronizado. Ya se aprobó visualmente. Evitar perder masks/animación o pasar a brillo horizontal.
- Última modificación del landing: ambas listas tienen scroll interno independiente con altura fija **32rem** desktop y **26rem** móvil. Títulos y enlaces All news/All resources fuera del área de scroll.
- Las guías de vinculación Discord se añadieron a Community en inglés, dos tarjetas con pasos numerados y enlaces, todo editable por JSON.
- Se corrigió el input del picker de videojuegos para usar bind oninput antes de disparar búsqueda: evita que renders de resultados escriban valores viejos sobre letras nuevas. **No se verificó aún reproduciendo tecleo rápido en navegador**.

### Markdown

Se habilitó en noticias, descripción de recursos, comentarios y notas de versión mediante MarkdownText; hay ayuda MarkdownHelp en los formularios. Se conserva texto fuente en DTOs/DB y al editar.

Parser Marked 18.1.1 + sanitizador DOMPurify 3.4.17 locales bajo `wwwroot/lib/markdown/`. `wwwroot/js/markdown.js` genera y sanea el fragmento; JS solo controla una hoja del DOM y Blazor su visibilidad. No usar MarkupString con input crudo.

Permite títulos, listas, citas, tablas, enlaces y código. HTML incrustado se muestra como texto; imágenes Markdown solo muestran el texto alternativo. Enlaces peligrosos/credenciales se bloquean; protocolos permitidos HTTP/HTTPS/mailto y rel noopener noreferrer. Si falla renderizado queda texto plano. Mantener CSS de tablas/código dentro del ancho y `::deep` para nodos insertados por JS. Actualizar dependencias con cuidado y conservar licencias.

## 12. Reglas, sello y contenido estático

Reglas, sello, comunidad, about, donaciones y privacidad usan JSON público y componentes de presentación. StaticDocumentPage carga el documento desde el navegador, maneja retry/cancelación y anclas. No necesitan tablas de reglas.

- Reglas por secciones con letras y numeración contextual. Índice debe navegar a anclas; números no son botones.
- Solo material pertinente a SNES. No material ilegal, hostigamiento, spam, enlaces maliciosos, previews engañosas ni subida sin permiso. Clasificar con precisión, acreditar todos los autores y aplicar flags sensibles.
- ROMs prohibidas salvo homebrew sin problemas de derechos; para modificaciones distribuir parches BPS/IPS, no la ROM original.
- Privados: acceso según autores/submitters/versiones y staff; no listados son públicos por enlace y pueden funcionar como dependencias. No confundir no listado con privado.
- Normativa: un recurso usado como dependencia no debe borrarse sin consultar al staff ni volverse privado. Siempre conservar al menos una versión. **Verificar implementación backend de estas reglas; el texto no las hace cumplir por sí solo.**
- Seal of Quality es una página separada de Rules. Certifica una **versión** revisada por el staff; solicitud optativa por checkbox. Si alguna versión tiene sello, resaltar recurso/nombre/lista y permitir filtro.
- Compromisos del sello: recurso público, restricciones de edición/borrado y contacto al staff para excepciones; restricciones relativas a participantes baneados según el JSON. No prometer que todo está impuesto automáticamente por el servicio.
- AlreadyModerated=false no debe producir un distintivo negativo. El panel completo de moderación/reportes quedó fuera de etapas iniciales.
- Donaciones promueven Patreon y desarrollo del sitio/proyectos SNES; incluyen canales supporters, votaciones de proyectos/mejoras y uso de emojis externos en Discord según la propuesta de comunidad.

## 13. Seguridad y deploy

Hay antiforgery global para acciones de escritura y header **X-CSRF-TOKEN**. Los fetch deben enviar el token cuando corresponda. Las APIs internas del bot tienen su propia autenticación/excepciones; revisar filtros concretos antes de cambiarlo.

Cloudflare **Turnstile** ya está integrado: sitekey pública, secret solo servidor, validación backend obligatoria. Algunas operaciones siempre piden captcha; comentarios/votos usan políticas por frecuencia. Un captcha puede aprobar automáticamente: no tiene por qué mostrar un reto difícil. El frontend preserva el formulario y reintenta tras la verificación.

Hay rate limiting configurado para login, register, password-email, reads, comments, votes, writes y uploads, con límite global de concurrencia de uploads. No tratar captcha como sustituto del rate limiting ni protección completa contra DDoS. La protección de borde/origen de Cloudflare se pospuso; retomarla cuando se solicite.

Secretos se configuran por entorno: user-secrets local y configuración privada/variables del hosting. No publicar passwords, JWTKey, Turnstile secret, secretos/tokens Discord ni credenciales DB. Hubo credenciales pegadas en conversaciones anteriores: **comprobar que se rotaron si siguen vigentes; no copiar sus valores al contexto**. Cada colaborador usa entorno propio o credenciales compartidas de desarrollo, nunca producción por defecto.

Ejemplos de claves de configuración relevantes, sin valores: ConnectionStrings:SNEStorageContext, JWTKey, DataProtection:KeyPath, Turnstile:SecretKey, APISettings:TurnstileSiteKey, APISettings:EmailPassword y la sección Discord. Comprobar clases/configuración actuales para el nombre preciso. En variables de entorno ASP.NET suele usarse `__` en vez de `:`.

Producción necesita tanto esquema/datos SQL como archivos del disco. Publicar DLLs no migra automáticamente la DB ni copia avatares/uploads existentes. Conservar Storage, avatares y DataProtection keys entre deploys. Los EmailTemplates tienen copia configurada en el csproj; appsettings.Development.json no se publica.

Se solucionó un DBError de perfiles en producción aplicando una migración que faltaba para campos Discord. El usuario confirmó que funcionó. Existir el UserId en DB no descarta un error de query/esquema; registrar la excepción real antes de diagnosticar.

Para generar un script idempotente desde la raíz:

```powershell
dotnet ef migrations script --idempotent --project SNEStorage/SNEStorage.csproj --output actualizar-db.sql
```

El archivo queda en el directorio donde se ejecutó el comando. El propietario lo pudo importar desde Monster.ASP cuando SSMS no conectaba. **Antes de aplicar, revisar script, historial de migraciones y backup.** Idempotente no significa que cualquier cambio preserve datos. No eliminar columnas ni marcar migraciones aplicadas a ciegas para ocultar diferencias.

## 14. Pendientes y límites del contexto

Esta lista es de continuidad, no una certificación de bugs aún presentes:

1. Confirmar en producción supervivencia del login a deploy/reinicio, renovación por actividad, ruta persistente de DataProtection y JWTKey estable. El mecanismo está en código; no se hizo aquí una prueba de larga duración.
2. Comprobar en navegador el tecleo rápido del picker tras el cambio de binding, Markdown, scroll fijo del landing y guías Community. Los últimos cambios no tuvieron una prueba visual completa.
3. Completar/revisar enforcement de normas de Seal of Quality, dependencias privadas/cambios de visibilidad/borrado y moderación. Definir las excepciones del staff antes de imponerlas.
4. Mantener limpieza periódica de batches/archivos huérfanos y política de retención/backups.
5. Retomar protección de borde contra DDoS y bloqueo del acceso directo al origen cuando corresponda.
6. Revisar permisos, validaciones, índices únicos de votos/relaciones y datos sensibles con el código actual; no asumir que todos los puntos de auditorías antiguas siguen pendientes.
7. El propietario **ya refactorizó AccountService y los demás servicios/controladores** al resultado común el 13 de octubre. No retomar la propuesta antigua como una tarea pendiente ni restaurar los bool/enums anteriores. La revisión actual está en `docs/revision-refactor-servicios-controladores-2026-10-13.md`; quedan correcciones de lógica allí descritas.
8. Teams y panel completo de moderación/reportes siguen fuera del alcance inicial. No generar rutas/contratos ficticios para funciones futuras.

La compilación de la web revisada el 13 de octubre terminó con **0 errores y 5 advertencias existentes**: nombre de migración fixtexts y propiedades sin inicializar en ResourceCategory/MemoryAddressValues. El bot también compiló. La solución completa todavía falla por 3 referencias obsoletas del proyecto de tests; no se ejecutaron tests. Ver la revisión de esa fecha para detalles. No implica que producción esté libre de fallos.

## 15. Documentos útiles y cómo retomar

- `docs/revision-refactor-servicios-controladores-2026-10-13.md`: revisión actual del resultado común, cambios de frontend y hallazgos de lógica ordenados por prioridad. Incluye un problema de ruta del avatar antes de validar el username que debe corregirse primero.
- `docs/markdown-frontend.md`: integración/saneamiento Markdown.
- READMEs de Components/Layout, Resources y Community: flujo frontend y archivos. Algunos párrafos antiguos de esos READMEs pueden conservar contratos previos; contrastarlos con controladores/DTOs.
- `docs/revision-servicios-controladores-2026-10-10.md`: auditoría anterior; revisar puntos contra código actual antes de repetirlos.
- `docs/diagnostico-rendimiento-2026-10-07.md` y `docs/propuesta-fixes-rendimiento-2026-10-08.md`: diagnóstico/propuestas históricas. Varias optimizaciones ya se aplicaron.
- `docs/persistencia-login-y-pendientes-2026-10-12.md`: propuesta histórica; **partes de cookies/security stamp/renovación ya están implementadas**, no usarla como estado vigente.
- Docs de antiforgery, captcha, rate limiting, validación de archivos y Discord: propuestas/guías para leer junto al código.

Mensaje sugerido para una conversación nueva:

> Lee docs/contexto-general-snestorage.md y los archivos actuales relacionados con la tarea. El backend lo modifico yo: puedes revisarlo y darme los cambios necesarios, pero tú implementas el frontend. Conserva los contratos y el diseño aprobado. No repitas pendientes históricos sin comprobar si ya se corrigieron. Empecemos por [tarea concreta].
