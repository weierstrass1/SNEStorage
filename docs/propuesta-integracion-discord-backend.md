# Propuesta de integración del backend de SNEStorage con Discord

Fecha: 9 de octubre de 2026  
Estado: propuesta; no implementada

## 1. Objetivo

Integrar SNEStorage con un servidor de Discord de forma que:

1. La estructura del servidor se pueda crear mediante `discord/server-config.json` en una operación administrativa explícita e idempotente.
2. Solamente puedan participar en el servidor personas con una cuenta activa de SNEStorage y un Discord vinculado de forma verificable.
3. El color visible del usuario en Discord se mantenga sincronizado con `UserInfo.UsernameColor`.
4. La creación de un recurso o de una versión publique un mensaje con enlace en `#resource-log`.

La autoridad de identidad debe ser SNEStorage. Discord es un sistema externo sincronizado, no una segunda fuente de verdad.

## 2. Problemas que debe resolver

### 2.1. El campo `UserInfo.Discord` no demuestra identidad

Actualmente el perfil contiene un campo de texto libre llamado `Discord`. Cualquier persona puede escribir allí cualquier nombre. Ese dato sirve como texto de perfil, pero no demuestra que la cuenta de Discord pertenezca al usuario de SNEStorage.

Para autorizar el acceso al servidor hace falta obtener el identificador inmutable de Discord mediante OAuth2 y asociarlo de manera única con el `UserId` autenticado de SNEStorage.

### 2.2. Una invitación de Discord no puede consultar SNEStorage

Discord no llama al backend de SNEStorage antes de aceptar una invitación normal. Por tanto, no es posible garantizar el requisito solamente configurando permisos o pegando una URL de invitación.

El flujo correcto es:

1. El usuario inicia sesión en SNEStorage.
2. Pulsa “Vincular Discord y entrar”.
3. Autoriza a la aplicación con OAuth2 usando los scopes `identify` y `guilds.join`.
4. SNEStorage verifica el resultado y guarda la relación entre ambas cuentas.
5. El bot agrega al usuario al servidor y le asigna sus roles.

Discord documenta que `guilds.join` permite utilizar el endpoint Add Guild Member y que el bot ya debe pertenecer al servidor. El endpoint acepta al usuario cuando existe un access token OAuth2 válido para ese usuario: [OAuth2 de Discord](https://discord.com/developers/docs/topics/oauth2) y [Guild Resource — Add Guild Member](https://docs.discord.com/developers/resources/guild#add-guild-member).

Una entrada por una invitación filtrada todavía es posible mientras existan invitaciones activas. Por ello el bot también debe escuchar las entradas al servidor y retirar o aislar a todo miembro que no tenga una vinculación válida.

### 2.3. El color de Discord no es una propiedad directa del miembro

Discord obtiene el color mostrado del rol con color que esté más alto en la jerarquía del usuario. No ofrece un campo “color de usuario”. Esto obliga a representar los colores de perfil mediante roles administrados por el bot. Discord confirma que el color visible proviene del rol coloreado más alto: [Roles and Permissions](https://support.discord.com/hc/en-us/articles/214836687-Discord-Roles-and-Permissions).

Esto tiene dos consecuencias:

- Los roles funcionales, como `SNEStorage Member` y `Moderator`, tendrían que ser neutros si todos los usuarios, incluidos moderadores, deben mostrar el color de su perfil.
- Discord limita un servidor a 250 roles. Un rol diferente para cada color arbitrario no escala sin límite: [Discord Account Caps, Server Caps, and More](https://support.discord.com/hc/en-us/articles/33694251638295-Discord-Account-Caps-Server-Caps-and-More).

La propuesta inicial usa un rol compartido por cada color hexadecimal en uso. Antes de implementar se debe elegir entre estas políticas:

- **Color exacto:** conserva cualquier `#RRGGBB`, pero funciona mientras la cantidad de colores únicos quepa dentro del presupuesto de roles.
- **Paleta limitada:** aproxima el color del perfil a una paleta de, por ejemplo, 32 o 64 colores. Es la opción escalable.

Para cumplir literalmente el requisito actual, esta propuesta asume color exacto con control de capacidad y limpieza de roles sin usuarios.

### 2.4. Publicar directamente durante una petición es frágil

`ResourceService.Create`, `ResourceService.AddVersion` y `AccountService.EditUserInfo` guardan cambios directamente con Entity Framework. Si esos métodos llamaran a Discord antes de confirmar la base de datos, podrían anunciar algo que finalmente no se guardó. Si llamaran después y Discord fallara, el dato quedaría guardado pero el aviso se perdería.

Se necesita una bandeja de salida persistente, u **outbox**. El cambio de negocio y el evento se guardan en la misma operación de base de datos; un proceso en segundo plano entrega el evento a Discord con reintentos e idempotencia.

### 2.5. El JSON todavía no configura nada por sí mismo

`discord/server-config.json` expresa la estructura deseada, pero Discord no importa ese formato directamente. Hace falta un provisionador que traduzca categorías, canales, roles, permisos y AutoMod a llamadas a la API de Discord.

La ejecución no debe ocurrir en cada arranque. Debe ser un comando administrativo explícito, seguro para volver a ejecutar y con un registro de los identificadores creados.

## 3. Decisiones funcionales recomendadas

Antes de escribir código conviene aprobar estas reglas:

| Tema | Decisión propuesta |
|---|---|
| Entrada normal | El acceso principal nace en SNEStorage mediante OAuth2; no se publica una invitación general. |
| Entrada no vinculada | Rol de cuarentena sin permisos y expulsión automática después de un margen corto, por ejemplo 60 segundos. |
| Bots ajenos | Expulsar cualquier cuenta con `user.bot = true`, excepto IDs incluidos en una allowlist administrativa. |
| Cuenta sin correo confirmado | No puede vincular Discord ni entrar. |
| Cuenta suspendida o baneada | Se retira del servidor. |
| Desvinculación | Se retira del servidor, no solamente el rol de miembro. |
| Color | Rol compartido por color hexadecimal exacto, mientras exista capacidad. |
| Recurso privado | No se anuncia. |
| Recurso público | Se anuncia después de confirmar la transacción. Se debe decidir si además exige moderación previa. |
| Nueva versión | Se anuncia una sola vez y enlaza al recurso correspondiente. |
| Configuración del servidor | Comando manual idempotente; nunca en cada inicio del sitio. |

Existe una inconsistencia en la configuración actual: `onAccountUnlink` está definido como `REMOVE_MEMBER_ROLE`, pero el requisito dice que solamente puede permanecer quien tenga Discord vinculado. Para mantener ese invariante debería cambiar a `REMOVE_FROM_SERVER`.

## 4. Arquitectura propuesta

Para el tamaño y la estructura actuales de SNEStorage, la primera versión puede vivir en la misma aplicación ASP.NET Core, pero separada en servicios claros:

```text
Navegador
   │
   │ sesión SNEStorage + OAuth2
   ▼
DiscordController ──► DiscordLinkService ──► Discord REST API
   │                         │
   │                         ▼
   └──────────────────► SQL Server
                             │
                     IntegrationOutbox
                             │
                             ▼
                   DiscordOutboxWorker ──► #resource-log / roles

Discord Gateway ──► DiscordMembershipWorker ──► vínculo en SQL Server
```

Separar estos componentes permite extraer los workers a un proceso independiente en el futuro sin cambiar los controladores ni el modelo de eventos.

No se recomienda que los controladores de recursos o de perfil conozcan detalles de HTTP de Discord. Solamente deben registrar eventos internos.

## 5. Cambios de datos

### 5.1. Nueva entidad `DiscordAccountLink`

No se recomienda agregar el ID verificado al campo de texto libre existente. Una entidad separada deja clara la diferencia entre “contacto escrito por el usuario” e “identidad comprobada por OAuth2”.

Campos propuestos:

| Campo | Tipo sugerido | Propósito |
|---|---|---|
| `UserId` | `nvarchar(450)`, PK y FK | Cuenta de ASP.NET Identity. |
| `DiscordUserId` | `varchar(20)`, índice único | Snowflake de Discord tratado como texto. |
| `DiscordUsername` | `nvarchar(100)` | Copia informativa; no se usa para autorizar. |
| `DiscordGlobalName` | `nvarchar(100)`, nullable | Nombre visible más reciente. |
| `LinkedAtUtc` | `datetime2` | Fecha de vinculación. |
| `LastVerifiedAtUtc` | `datetime2` | Última conciliación exitosa. |
| `MembershipState` | `tinyint` o string controlado | `Linked`, `Joined`, `Removed`, `Error`. |
| `LastColorRoleId` | `varchar(20)`, nullable | Último rol de color aplicado. |

Restricciones necesarias:

- Un `UserId` solamente puede tener un Discord vinculado.
- Un `DiscordUserId` solamente puede pertenecer a un `UserId`.
- El ID de Discord nunca se compara por nombre de usuario.
- No se guardan access tokens ni refresh tokens si solamente se necesitan para ejecutar el alta inmediata.

El campo actual `UserInfo.Discord` puede conservarse como texto público durante una transición, pero no debe intervenir en decisiones de acceso. A largo plazo conviene ocultarlo o reemplazarlo en la interfaz por el estado de la vinculación verificada.

### 5.2. Nueva entidad `IntegrationOutboxEvent`

Campos mínimos:

| Campo | Propósito |
|---|---|
| `Id` | GUID que también funciona como clave de idempotencia. |
| `EventType` | `discord.profile_color_changed`, `resource.created`, `resource.version.created`, etc. |
| `AggregateId` | ID del usuario, recurso o versión. |
| `PayloadJson` | Datos mínimos e inmutables necesarios para procesar el evento. |
| `CreatedAtUtc` | Orden y diagnóstico. |
| `AvailableAtUtc` | Próximo intento permitido. |
| `ProcessedAtUtc` | Marca de éxito. |
| `AttemptCount` | Control de reintentos. |
| `LastError` | Error resumido sin secretos. |
| `LockedUntilUtc` | Evita que dos workers procesen el mismo evento. |

Índices recomendados:

- `(ProcessedAtUtc, AvailableAtUtc)` para buscar pendientes.
- Índice único opcional sobre una clave lógica, por ejemplo `resource.created:{resourceId}`.

### 5.3. Nueva entidad `DiscordManagedResource`

Relaciona las claves lógicas del JSON con los snowflakes reales de Discord:

| Campo | Ejemplo |
|---|---|
| `GuildId` | ID del servidor. |
| `ResourceType` | `Role`, `Category`, `Channel`, `AutoModRule`. |
| `ResourceKey` | `info.resource-log`, `member`, `staff`. |
| `DiscordResourceId` | ID creado por Discord. |
| `ConfigSchemaVersion` | Versión aplicada del JSON. |
| `UpdatedAtUtc` | Última conciliación. |

Esto evita buscar elementos solamente por nombre y permite cambiarles el nombre sin duplicarlos.

### 5.4. Nueva entidad `DiscordColorRole`

Campos propuestos:

- `ColorHex`, clave única normalizada como `#RRGGBB`.
- `DiscordRoleId`, único.
- `LastUsedAtUtc`.
- `MarkedForDeletionAtUtc`, nullable.

El número de usuarios se puede calcular al reconciliar. Un rol sin miembros puede eliminarse después de un periodo de gracia, no inmediatamente, para evitar crear y borrar el mismo rol repetidamente.

### 5.5. Migración de Entity Framework

La migración debe crear las cuatro tablas, claves foráneas e índices únicos. No debe convertir automáticamente el texto libre `UserInfo.Discord` en vínculos verificados: esos valores no prueban propiedad y podrían vincular la cuenta equivocada.

## 6. Configuración y secretos

Agregar una sección de opciones tipadas, por ejemplo `DiscordOptions`, con valores no secretos:

```json
{
  "Discord": {
    "Enabled": false,
    "ApplicationId": "...",
    "GuildId": "...",
    "PublicBaseUrl": "https://snestorage.dev",
    "OAuthCallbackPath": "/api/discord/link/callback",
    "ServerConfigPath": "../discord/server-config.json",
    "MemberRoleKey": "member",
    "QuarantineRoleKey": "unverified",
    "ResourceLogChannelKey": "info.resource-log"
  }
}
```

`BotToken` y `ClientSecret` no deben aparecer en `appsettings.json`, Git, logs ni respuestas HTTP. Deben venir de variables de entorno, secretos del proveedor de hosting o un almacén de secretos:

- `Discord__BotToken`
- `Discord__ClientSecret`

El arranque debe validar que los valores obligatorios existan cuando `Discord:Enabled` sea `true`. En desarrollo, `Enabled=false` permite trabajar sin conectarse a Discord.

## 7. Servicios nuevos

### 7.1. `DiscordApiClient`

Responsabilidad: encapsular la API REST de Discord.

Operaciones mínimas:

- Intercambiar el código OAuth2 por un access token.
- Consultar `/users/@me`.
- Agregar miembro al guild.
- Obtener, asignar y retirar roles.
- Crear o actualizar roles, categorías, canales y reglas administradas.
- Retirar un miembro.
- Publicar mensajes o embeds.

Debe usar `HttpClientFactory`, timeouts, cancelación y manejo de rate limits. Discord exige respetar `Retry-After` ante un `429`; además aplica límites por ruta y un límite global: [Rate Limits](https://docs.discord.com/developers/topics/rate-limits).

Las operaciones deben distinguir al menos:

- error transitorio: timeout, `429`, `5xx`;
- error permanente: `400`, recurso inexistente confirmado;
- configuración o permisos inválidos: `401`, `403`;
- conflicto idempotente: el usuario o recurso ya existe.

### 7.2. `DiscordLinkService`

Responsabilidad: vincular una cuenta autenticada de SNEStorage con una identidad de Discord.

Debe:

1. Comprobar cuenta activa, correo confirmado y ausencia de ban.
2. Generar un `state` criptográficamente aleatorio, de un solo uso y con expiración corta.
3. Asociar ese `state` con el `UserId` autenticado en caché o sesión del servidor.
4. Construir la autorización con `identify guilds.join`.
5. En el callback, validar `state` antes de intercambiar el código.
6. Consultar el usuario actual de Discord.
7. Rechazar IDs ya asociados con otra cuenta.
8. Guardar el vínculo y agregar el miembro.
9. Asignar los roles de miembro y color.
10. Descartar el access token al terminar.

La transacción de base de datos y la llamada externa no pueden ser una sola transacción distribuida. Por eso el vínculo debe quedar en un estado explícito y recuperable. El endpoint Add Guild Member necesita el access token del usuario; si se decide no persistirlo, un worker no puede completar el ingreso por sí solo. La implementación propuesta guarda el vínculo como `LinkedNotJoined`, intenta el alta inmediatamente y, si falla después de reintentos breves, pide al usuario repetir la autorización para obtener un token nuevo. Así no se almacenan tokens OAuth2.

### 7.3. `DiscordMembershipService`

Responsabilidad: aplicar el invariante “miembro de Discord = cuenta válida de SNEStorage vinculada”.

Reglas:

- Si llega un miembro conocido y válido, asignar `SNEStorage Member` y sincronizar color.
- Si llega una persona sin vínculo, asignar cuarentena y programar expulsión.
- Si llega un bot no autorizado, expulsarlo.
- Si una cuenta se banea, desactiva o desvincula, retirar al miembro.
- Si el bot pierde permisos, registrar una alerta operativa y no entrar en un bucle agresivo.

Para reaccionar inmediatamente se necesita el evento `Guild Member Add`, que requiere el intent privilegiado `GUILD_MEMBERS`: [Gateway Events — Guild Member Add](https://docs.discord.com/developers/events/gateway-events#guild-member-add) y [Privileged Intents](https://support-dev.discord.com/hc/en-us/articles/6207308062871-What-are-Privileged-Intents).

Además del evento en tiempo real debe existir una conciliación periódica, por ejemplo cada 15 minutos, que repare eventos perdidos y retire accesos inválidos. No se recomienda recorrer todo el servidor cada 60 segundos.

### 7.4. `DiscordColorRoleService`

Responsabilidad: traducir `UsernameColor` a un rol administrado.

Algoritmo propuesto:

1. Validar y normalizar el color a `#RRGGBB`.
2. Buscar `DiscordColorRole` por color.
3. Si no existe y hay presupuesto, crear un rol llamado `color-rrggbb`, sin permisos, sin `hoist` y sin menciones.
4. Colocar los roles de color por debajo del rol del bot y por encima de los roles funcionales sin color.
5. Asignar el nuevo rol al miembro.
6. Retirar cualquier otro rol administrado de color que tenga ese miembro.
7. Marcar roles sin uso para limpieza diferida.

Discord exige `MANAGE_ROLES` para crear y asignar roles, y el bot solamente puede administrar roles inferiores a su rol más alto: [Guild Resource — roles](https://docs.discord.com/developers/resources/guild#create-guild-role).

Si se alcanza el presupuesto configurado, el servicio no debe borrar roles funcionales ni fallar silenciosamente. Debe conservar el color anterior, marcar el evento como error operativo y avisar en `#automod-alerts` o en el sistema de monitoreo.

### 7.5. `DiscordOutboxWorker`

Responsabilidad: entregar eventos pendientes de forma confiable.

Comportamiento:

- Leer lotes pequeños de eventos disponibles.
- Bloquear cada evento durante el procesamiento.
- Reintentar errores transitorios con espera exponencial y jitter.
- Respetar `Retry-After` de Discord.
- Marcar éxito solamente después de recibir una respuesta válida.
- Mover a estado de intervención manual después de un máximo de intentos.
- Evitar mensajes duplicados mediante una clave de idempotencia y, para anuncios, guardar el ID del mensaje publicado.

### 7.6. `DiscordServerProvisioner`

Responsabilidad: aplicar `discord/server-config.json` cuando un administrador lo solicite.

Se recomienda exponerlo como comando local, no como endpoint público:

```text
dotnet run --project SNEStorage -- discord setup --config ../discord/server-config.json
```

El comando debe:

1. Validar completamente el JSON antes de tocar Discord.
2. Confirmar que `GuildId` coincide con el servidor esperado.
3. Crear o actualizar roles, categorías, canales, permisos y AutoMod en orden de dependencia.
4. Guardar cada ID en `DiscordManagedResource`.
5. Mostrar un resumen de cambios.
6. Guardar `ConfigSchemaVersion` solamente al finalizar correctamente.

“Una sola vez” no debería significar que no se puede reparar. El comando debe ser idempotente: una segunda ejecución sin cambios produce cero cambios; una ejecución posterior con una versión nueva aplica solamente la diferencia.

No debe borrar canales, roles o mensajes desconocidos de forma automática. Las eliminaciones deben requerir una opción explícita y un informe previo.

## 8. Endpoints nuevos

### Endpoints autenticados para el usuario

| Método | Ruta | Resultado |
|---|---|---|
| `GET` | `/api/discord/link/status` | Estado de vínculo y pertenencia, sin revelar tokens. |
| `GET` | `/api/discord/link/start` | Redirección a OAuth2. |
| `GET` | `/api/discord/link/callback` | Valida el callback, vincula y agrega al servidor. |
| `DELETE` | `/api/discord/link` | Desvincula y programa la retirada del servidor. |

El callback no debe aceptar un `UserId` enviado por el navegador. El usuario se recupera exclusivamente del `state` seguro generado al iniciar el flujo.

### Endpoints operativos opcionales

Si existe una necesidad real de administración remota, se podrían agregar endpoints protegidos por la política `Admin` para consultar salud y reintentar eventos. No se recomienda publicar un endpoint que ejecute el setup completo del servidor.

## 9. Cambios en puntos existentes del backend

### `Program.cs`

- Registrar opciones validadas.
- Registrar `DiscordApiClient` con `HttpClientFactory`.
- Registrar los servicios de vínculo, membresía, colores y provisioning.
- Registrar `DiscordOutboxWorker` y el cliente Gateway solamente cuando la integración esté habilitada.
- Configurar el apagado limpio de los workers.
- No impedir que el sitio arranque en desarrollo cuando Discord esté deshabilitado.

### `SnestorageContext.cs`

- Agregar `DbSet` para las entidades nuevas.
- Registrar sus configuraciones de Entity Framework.

### `AccountService.EditUserInfo`

- Normalizar y validar `UsernameColor`.
- Comparar el color anterior con el nuevo.
- Si cambió y existe vínculo, agregar `discord.profile_color_changed` al outbox antes del mismo `SaveChangesAsync`.
- No llamar directamente a Discord desde esta petición.

### `ResourceService.Create`

- Agregar `resource.created` al outbox junto con el nuevo recurso antes del `SaveChangesAsync` que le asigna el ID.
- El payload puede guardar el ID después de que EF lo genere o referenciar la entidad mediante una relación interna.
- El worker debe volver a leer el recurso y comprobar su visibilidad antes de publicarlo.
- No anunciar datos privados, borradores ni recursos no aptos para visualización pública.

### `ResourceService.AddVersion`

- Agregar `resource.version.created` en la misma unidad de trabajo que la nueva versión.
- Usar como clave lógica `resource.version.created:{versionId}` para impedir duplicados.

### Ban, desactivación y eliminación de cuenta

- Los flujos que invaliden una cuenta deben crear `discord.membership.revoked`.
- La eliminación de datos debe conservar solamente lo necesario para auditoría y cumplir la política de privacidad.

### Perfil de usuario

- Mostrar el estado real de vinculación.
- Cambiar la edición manual de `Discord` por botones “Vincular”, “Volver a vincular” y “Desvincular”, o dejar el campo manual claramente etiquetado como texto no verificado durante la transición.

## 10. Mensaje de `#resource-log`

El worker debe construir el enlace canónico así:

```text
{PublicBaseUrl}/Resource/{resourceId}
```

Contenido recomendado del embed:

- Título del recurso.
- Enlace al recurso.
- Juego.
- Tipos o categoría.
- Autores.
- Descripción recortada con seguridad.
- Primera imagen previa válida, si existe.
- Advertencias de contenido.
- Nombre del usuario que lo publicó.

No se debe mencionar `@everyone` ni aceptar Markdown sin controlar proveniente del recurso. Se deben neutralizar menciones mediante `allowed_mentions` vacío.

## 11. Ajustes necesarios en `server-config.json`

Aunque no son cambios de backend, son necesarios para que la implementación cumpla los requisitos:

1. Agregar un rol `unverified` o diseñar `@everyone` como cuarentena por defecto.
2. Cambiar `onAccountUnlink` a `REMOVE_FROM_SERVER`.
3. Dejar sin color los roles funcionales si el color de perfil debe prevalecer para todos.
4. Confirmar que el rol del bot queda por encima de `member`, `unverified` y todos los roles `color-*`.
5. Eliminar `CreateInstantInvite` de los permisos del bot si no se usa.
6. Eliminar `ManageWebhooks` si los mensajes se publican como bot, tal como indica `delivery: BOT`.
7. Mantener `KickMembers`, `ManageRoles`, permisos de canales/configuración para setup y envío de mensajes.

Conviene separar los permisos de ejecución normal de los permisos de setup. El setup puede utilizar credenciales administrativas temporalmente; el bot cotidiano debe conservar solamente los permisos que necesita.

## 12. Seguridad y privacidad

- Validar `state` OAuth2, hacerlo de un solo uso y darle expiración corta.
- Utilizar HTTPS y una redirect URI exacta registrada en Discord.
- No registrar códigos OAuth2, access tokens, bot tokens ni client secrets.
- No guardar el access token del usuario después del alta si no existe otra necesidad.
- Cifrar cualquier token persistente si en el futuro se decide conservarlo.
- Aplicar índices únicos para impedir carreras de doble vinculación.
- Utilizar protección CSRF en la desvinculación.
- Registrar auditoría de vínculo, desvínculo, alta, retirada y errores de permisos.
- Añadir a la política de privacidad el ID de Discord almacenado, la finalidad y el proceso de eliminación.
- No conceder `Administrator` al bot.
- Mantener una allowlist explícita para bots autorizados.

## 13. Fiabilidad e idempotencia

Cada operación debe tolerar reintentos:

- Vincular nuevamente el mismo par de cuentas devuelve éxito sin duplicar filas.
- Agregar un usuario que ya está en el servidor continúa con la asignación de roles.
- Asignar un rol ya presente no se considera error.
- Procesar dos veces `resource.created` no crea dos mensajes.
- Ejecutar setup dos veces no duplica canales ni roles.
- Recibir el mismo `Guild Member Add` más de una vez no duplica trabajo.

Discord advierte que el evento de alta puede recibirse incluso para usuarios que ya eran miembros, por lo que el manejador debe ser idempotente: [Gateway Events](https://docs.discord.com/developers/events/gateway-events#guild-member-add).

## 14. Observabilidad

Métricas o contadores mínimos:

- Vínculos iniciados, completados y rechazados.
- Miembros aceptados, puestos en cuarentena y retirados.
- Eventos pendientes y eventos agotados en outbox.
- Tiempo entre creación de recurso y publicación en Discord.
- Roles de color activos y presupuesto restante.
- Respuestas `401`, `403`, `429` y `5xx` de Discord.

Los logs deben incluir `EventId`, `DiscordUserId` parcialmente enmascarado, `UserId`, tipo de operación y código HTTP, pero nunca credenciales.

Se recomienda un health check degradado para Discord. Una caída de Discord no debe tumbar el sitio ni impedir crear recursos; solamente retrasa la sincronización.

## 15. Plan de implementación por fases

### Fase 0: decisiones

- Aprobar color exacto frente a paleta.
- Decidir si se anuncia al crear o después de moderación.
- Confirmar expulsión al desvincular.
- Definir el margen de cuarentena.

### Fase 1: base técnica

- Crear opciones, modelos, configuraciones EF y migración.
- Implementar `DiscordApiClient` con pruebas sobre un servidor de desarrollo.
- Implementar outbox y worker sin conectar todavía los flujos de negocio.

### Fase 2: vinculación e ingreso

- Agregar endpoints OAuth2.
- Implementar restricciones de unicidad y estados recuperables.
- Agregar usuario con `guilds.join`.
- Asignar miembro y ejecutar conciliación.

### Fase 3: control de entradas

- Activar `GUILD_MEMBERS` en Developer Portal y en el cliente Gateway.
- Implementar cuarentena, allowlist de bots y retirada automática.
- Añadir conciliación periódica.

### Fase 4: colores

- Crear roles de color y jerarquía.
- Emitir evento al editar perfil.
- Añadir limpieza diferida y alarma de capacidad.

### Fase 5: publicaciones de recursos

- Emitir eventos desde creación de recurso y versión.
- Construir embeds seguros.
- Añadir idempotencia y política de visibilidad/moderación.

### Fase 6: setup del servidor

- Implementar validación del JSON y dry run.
- Aplicar la configuración en un guild de pruebas.
- Ejecutar una vez en producción.
- Reducir después los permisos cotidianos del bot.

## 16. Pruebas y criterios de aceptación

### Vinculación

- Un usuario sin sesión de SNEStorage no puede iniciar el vínculo.
- Un `state` inválido, expirado o reutilizado es rechazado.
- Un Discord no puede vincularse a dos cuentas.
- Una cuenta baneada o sin correo confirmado no puede entrar.
- El token OAuth2 no queda almacenado ni registrado.

### Acceso al servidor

- Una vinculación válida agrega al usuario y asigna `SNEStorage Member`.
- Una entrada por invitación sin vínculo no obtiene acceso a canales de comunidad y termina retirada.
- Un bot no autorizado termina retirado.
- Desvincular o banear una cuenta retira al miembro.
- La caída temporal de Discord se recupera mediante reintentos.

### Color

- Cambiar `UsernameColor` produce un único evento.
- El usuario recibe el rol correspondiente al nuevo color y pierde el anterior.
- Dos usuarios con el mismo color comparten rol.
- Los roles funcionales no ocultan el color del perfil.
- Alcanzar el límite de roles produce una alerta controlada, no corrupción de roles.

### Recursos

- Un recurso confirmado y publicable genera exactamente un mensaje.
- Un error de Discord deja el evento pendiente para reintento.
- Un recurso privado no se filtra al canal.
- El enlace apunta a `/Resource/{id}`.
- La descripción no puede generar menciones masivas.

### Setup

- Un dry run no modifica el servidor.
- La primera ejecución crea lo esperado.
- La segunda ejecución sin cambios no crea duplicados.
- Un fallo parcial puede reanudarse.
- Ningún recurso no administrado se elimina automáticamente.

## 17. Riesgos principales

| Riesgo | Mitigación |
|---|---|
| Agotamiento de roles por colores | Presupuesto, roles compartidos, limpieza y futura paleta limitada. |
| Jerarquía incorrecta | Validación previa y health check del rol del bot. |
| Invitación filtrada | Cuarentena por defecto, evento de entrada y expulsión. |
| Discord caído o limitado | Outbox, backoff, `Retry-After` y conciliación. |
| Mensajes duplicados | Clave lógica e ID del mensaje persistido. |
| Vinculación robada | OAuth2 con `state` de un solo uso y sesión SNEStorage obligatoria. |
| Filtración de recursos privados | El worker vuelve a consultar visibilidad antes de publicar. |
| El bot afecta disponibilidad web | Fallos aislados, workers resilientes y posterior extracción a proceso separado. |

## 18. Alcance mínimo recomendado para la primera entrega

La primera versión debería incluir:

1. Vinculación OAuth2 verificada.
2. Alta mediante `guilds.join`.
3. Rechazo de entradas no vinculadas.
4. Rol de miembro.
5. Sincronización de color con límite explícito.
6. Outbox para color y recursos.
7. Publicación de recursos públicos.
8. Setup idempotente y manual.
9. Auditoría y reintentos.

No es necesario incluir comandos slash, sincronización de presencia, lectura de mensajes ni moderación por contenido propia del bot. Discord AutoMod y la configuración nativa del servidor cubren esa parte sin pedir el intent de contenido de mensajes.

## 19. Resultado esperado

Al finalizar la implementación, SNEStorage controlará el acceso al servidor sin confiar en nombres escritos manualmente. Las fallas temporales de Discord no romperán la creación de recursos ni la edición de perfiles. El servidor podrá configurarse desde el JSON de manera repetible, y cada cambio automático quedará registrado y podrá repararse mediante conciliación.

## 20. Propuesta de implementación en código

Los siguientes fragmentos son una propuesta concreta adaptada a la estructura actual de SNEStorage en .NET 10. No han sido aplicados al proyecto. Los nombres de migración y namespaces se pueden ajustar, pero las restricciones de seguridad e idempotencia no deberían omitirse.

### 20.1. Estructura de archivos propuesta

```text
SNEStorage/
├─ Controllers/
│  └─ DiscordController.cs
├─ Discord/
│  ├─ DiscordOptions.cs
│  ├─ DiscordApiClient.cs
│  ├─ DiscordLinkService.cs
│  ├─ DiscordMembershipService.cs
│  ├─ DiscordColorRoleService.cs
│  ├─ DiscordOutboxWorker.cs
│  ├─ DiscordOutboxProcessor.cs
│  ├─ DiscordGatewayWorker.cs
│  ├─ DiscordServerProvisioner.cs
│  └─ DiscordModels.cs
├─ Models/
│  ├─ DiscordAccountLink.cs
│  ├─ IntegrationOutboxEvent.cs
│  ├─ DiscordManagedResource.cs
│  ├─ DiscordColorRole.cs
│  └─ Builds/
│     ├─ DiscordAccountLinkBuild.cs
│     ├─ IntegrationOutboxEventBuild.cs
│     ├─ DiscordManagedResourceBuild.cs
│     └─ DiscordColorRoleBuild.cs
└─ DTOs/
   └─ DiscordLinkStatusDto.cs
```

### 20.2. Opciones tipadas

Archivo propuesto: `SNEStorage/Discord/DiscordOptions.cs`.

```csharp
namespace SNEStorage.Discord;

public sealed class DiscordOptions
{
    public const string SectionName = "Discord";

    public bool Enabled { get; init; }
    public string ApplicationId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
    public string BotToken { get; init; } = "";
    public string GuildId { get; init; } = "";
    public string PublicBaseUrl { get; init; } = "https://snestorage.dev";
    public string OAuthCallbackPath { get; init; } = "/api/discord/link/callback";
    public string ServerConfigPath { get; init; } = "../discord/server-config.json";
    public string MemberRoleKey { get; init; } = "member";
    public string QuarantineRoleKey { get; init; } = "unverified";
    public string ResourceLogChannelKey { get; init; } = "info.resource-log";
    public int UnauthorizedJoinGraceSeconds { get; init; } = 60;
    public int ReconciliationMinutes { get; init; } = 15;
    public int MaxManagedColorRoles { get; init; } = 180;
    public string[] AllowedBotUserIds { get; init; } = [];

    public Uri CallbackUri => new(
        new Uri(PublicBaseUrl.TrimEnd('/') + "/"),
        OAuthCallbackPath.TrimStart('/'));
}
```

En producción `ClientSecret` y `BotToken` se inyectan mediante variables de entorno o el sistema de secretos del hosting. Aunque aparezcan como propiedades de opciones, no se escriben en el JSON versionado.

### 20.3. Entidad de vínculo verificado

Archivo propuesto: `SNEStorage/Models/DiscordAccountLink.cs`.

```csharp
using System.Text.Json.Serialization;

namespace SNEStorage.Models;

public enum DiscordMembershipState : byte
{
    LinkedNotJoined = 1,
    Joined = 2,
    Removed = 3,
    Error = 4
}

public partial class DiscordAccountLink
{
    public required string UserId { get; set; }
    public required string DiscordUserId { get; set; }
    public required string DiscordUsername { get; set; }
    public string? DiscordGlobalName { get; set; }
    public DateTime LinkedAtUtc { get; set; }
    public DateTime? LastVerifiedAtUtc { get; set; }
    public DiscordMembershipState MembershipState { get; set; }
    public string? LastColorRoleId { get; set; }

    [JsonIgnore]
    public virtual ApplicationUser? User { get; set; }
}
```

Agregar a `ApplicationUser`:

```csharp
public virtual DiscordAccountLink? DiscordAccountLink { get; set; }
```

Mapeo propuesto: `SNEStorage/Models/Builds/DiscordAccountLinkBuild.cs`.

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace SNEStorage.Models;

public partial class DiscordAccountLink
{
    public static void Build(EntityTypeBuilder<DiscordAccountLink> entity)
    {
        entity.ToTable("discord_account_link");

        entity.HasKey(e => e.UserId)
            .HasName("PK_discord_account_link");

        entity.Property(e => e.UserId)
            .HasColumnType("nvarchar(450)")
            .HasColumnName("user_id");

        entity.Property(e => e.DiscordUserId)
            .HasColumnType("varchar(20)")
            .HasColumnName("discord_user_id");

        entity.HasIndex(e => e.DiscordUserId)
            .IsUnique()
            .HasDatabaseName("UX_discord_account_link_discord_user_id");

        entity.Property(e => e.DiscordUsername)
            .HasMaxLength(100)
            .HasColumnName("discord_username");

        entity.Property(e => e.DiscordGlobalName)
            .HasMaxLength(100)
            .HasColumnName("discord_global_name");

        entity.Property(e => e.LinkedAtUtc)
            .HasColumnName("linked_at_utc");

        entity.Property(e => e.LastVerifiedAtUtc)
            .HasColumnName("last_verified_at_utc");

        entity.Property(e => e.MembershipState)
            .HasConversion<byte>()
            .HasColumnName("membership_state");

        entity.Property(e => e.LastColorRoleId)
            .HasColumnType("varchar(20)")
            .HasColumnName("last_color_role_id");

        entity.HasOne(e => e.User)
            .WithOne(e => e.DiscordAccountLink)
            .HasForeignKey<DiscordAccountLink>(e => e.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("FK_discord_account_link_user");
    }
}
```

### 20.4. Entidad outbox

La propuesta agrega referencias opcionales a usuario, recurso y versión. Esto permite que Entity Framework inserte el recurso y su evento en un solo `SaveChangesAsync`, aun cuando el ID del recurso todavía no exista al construir los objetos.

Archivo propuesto: `SNEStorage/Models/IntegrationOutboxEvent.cs`.

```csharp
namespace SNEStorage.Models;

public static class IntegrationEventTypes
{
    public const string ProfileColorChanged = "discord.profile_color_changed";
    public const string MembershipRevoked = "discord.membership_revoked";
    public const string ResourceCreated = "resource.created";
    public const string ResourceVersionCreated = "resource.version.created";
}

public sealed class IntegrationOutboxEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string EventType { get; set; }
    public string? UserId { get; set; }
    public long? ResourceId { get; set; }
    public long? ResourceVersionId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime AvailableAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAtUtc { get; set; }
    public DateTime? LockedUntilUtc { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public string? DiscordMessageId { get; set; }

    public ApplicationUser? User { get; set; }
    public Resource? Resource { get; set; }
    public ResourceVersion? ResourceVersion { get; set; }

    public static IntegrationOutboxEvent ForProfileColor(
        string userId,
        string normalizedColor) => new()
    {
        EventType = IntegrationEventTypes.ProfileColorChanged,
        UserId = userId,
        PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            color = normalizedColor
        })
    };

    public static IntegrationOutboxEvent ForResource(Resource resource) => new()
    {
        EventType = IntegrationEventTypes.ResourceCreated,
        Resource = resource
    };

    public static IntegrationOutboxEvent ForVersion(ResourceVersion version) => new()
    {
        EventType = IntegrationEventTypes.ResourceVersionCreated,
        ResourceVersion = version,
        Resource = version.Resource
    };
}
```

Mapeo resumido:

```csharp
public static void Build(EntityTypeBuilder<IntegrationOutboxEvent> entity)
{
    entity.ToTable("integration_outbox_event");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.EventType).HasMaxLength(100).IsRequired();
    entity.Property(e => e.PayloadJson).HasColumnType("nvarchar(max)");
    entity.Property(e => e.LastError).HasColumnType("nvarchar(2000)");
    entity.Property(e => e.DiscordMessageId).HasColumnType("varchar(20)");

    entity.HasIndex(e => new { e.ProcessedAtUtc, e.AvailableAtUtc });

    entity.HasOne(e => e.Resource)
        .WithMany()
        .HasForeignKey(e => e.ResourceId)
        .OnDelete(DeleteBehavior.SetNull);

    entity.HasOne(e => e.ResourceVersion)
        .WithMany()
        .HasForeignKey(e => e.ResourceVersionId)
        .OnDelete(DeleteBehavior.SetNull);

    entity.HasOne(e => e.User)
        .WithMany()
        .HasForeignKey(e => e.UserId)
        .OnDelete(DeleteBehavior.SetNull);

    entity.HasIndex(e => new { e.EventType, e.ResourceId })
        .IsUnique()
        .HasFilter("[resource_id] IS NOT NULL AND [event_type] = 'resource.created'");

    entity.HasIndex(e => new { e.EventType, e.ResourceVersionId })
        .IsUnique()
        .HasFilter("[resource_version_id] IS NOT NULL AND [event_type] = 'resource.version.created'");
}
```

El índice filtrado exacto debe revisarse en la migración generada porque SQL Server aplica el filtro como texto SQL.

### 20.5. Entidades de recursos administrados y colores

```csharp
namespace SNEStorage.Models;

public sealed class DiscordManagedResource
{
    public long Id { get; set; }
    public required string GuildId { get; set; }
    public required string ResourceType { get; set; }
    public required string ResourceKey { get; set; }
    public required string DiscordResourceId { get; set; }
    public int ConfigSchemaVersion { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}

public sealed class DiscordColorRole
{
    public required string ColorHex { get; set; }
    public required string DiscordRoleId { get; set; }
    public DateTime LastUsedAtUtc { get; set; }
    public DateTime? MarkedForDeletionAtUtc { get; set; }
}
```

Restricciones de mapeo:

```csharp
entity.HasIndex(e => new { e.GuildId, e.ResourceType, e.ResourceKey })
    .IsUnique();

entity.HasIndex(e => e.DiscordResourceId)
    .IsUnique();

// Para DiscordColorRole:
entity.HasKey(e => e.ColorHex);
entity.HasIndex(e => e.DiscordRoleId).IsUnique();
```

### 20.6. Cambios en `SnestorageContext`

Agregar propiedades:

```csharp
public virtual DbSet<DiscordAccountLink> DiscordAccountLinks { get; set; }
public virtual DbSet<IntegrationOutboxEvent> IntegrationOutboxEvents { get; set; }
public virtual DbSet<DiscordManagedResource> DiscordManagedResources { get; set; }
public virtual DbSet<DiscordColorRole> DiscordColorRoles { get; set; }
```

Agregar en `OnModelCreating`:

```csharp
modelBuilder.Entity<DiscordAccountLink>(DiscordAccountLink.Build);
modelBuilder.Entity<IntegrationOutboxEvent>(IntegrationOutboxEvent.Build);
modelBuilder.Entity<DiscordManagedResource>(DiscordManagedResource.Build);
modelBuilder.Entity<DiscordColorRole>(DiscordColorRole.Build);
```

### 20.7. Modelos de la API de Discord

Archivo propuesto: `SNEStorage/Discord/DiscordModels.cs`.

```csharp
using System.Text.Json.Serialization;

namespace SNEStorage.Discord;

public sealed record DiscordTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("scope")] string Scope);

public sealed record DiscordCurrentUser(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("global_name")] string? GlobalName,
    [property: JsonPropertyName("bot")] bool Bot);

public sealed record DiscordCreatedMessage(
    [property: JsonPropertyName("id")] string Id);
```

### 20.8. Cliente REST de Discord

Archivo propuesto: `SNEStorage/Discord/DiscordApiClient.cs`.

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace SNEStorage.Discord;

public sealed class DiscordApiClient(
    HttpClient http,
    IOptions<DiscordOptions> options,
    ILogger<DiscordApiClient> logger)
{
    private readonly HttpClient _http = http;
    private readonly DiscordOptions _options = options.Value;
    private readonly ILogger<DiscordApiClient> _logger = logger;

    public async Task<DiscordTokenResponse> ExchangeCodeAsync(
        string code,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "https://discord.com/api/v10/oauth2/token")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _options.ApplicationId,
                ["client_secret"] = _options.ClientSecret,
                ["grant_type"] = "authorization_code",
                ["code"] = code,
                ["redirect_uri"] = _options.CallbackUri.ToString()
            })
        };

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content
            .ReadFromJsonAsync<DiscordTokenResponse>(cancellationToken: ct))!;
    }

    public async Task<DiscordCurrentUser> GetCurrentUserAsync(
        string accessToken,
        CancellationToken ct)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://discord.com/api/v10/users/@me");
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content
            .ReadFromJsonAsync<DiscordCurrentUser>(cancellationToken: ct))!;
    }

    public async Task AddGuildMemberAsync(
        string discordUserId,
        string userAccessToken,
        CancellationToken ct)
    {
        using var request = BotRequest(
            HttpMethod.Put,
            $"guilds/{_options.GuildId}/members/{discordUserId}");
        request.Content = JsonContent.Create(new
        {
            access_token = userAccessToken
        });

        using var response = await SendWithRateLimitRetryAsync(request, ct);
        // Discord devuelve 201 si lo agrega y 204 si ya era miembro.
        if (response.StatusCode is not System.Net.HttpStatusCode.Created and
            not System.Net.HttpStatusCode.NoContent)
        {
            throw new HttpRequestException(
                $"Discord Add Guild Member returned {(int)response.StatusCode}.");
        }
    }

    public async Task AddRoleAsync(
        string discordUserId,
        string roleId,
        CancellationToken ct)
    {
        using var request = BotRequest(
            HttpMethod.Put,
            $"guilds/{_options.GuildId}/members/{discordUserId}/roles/{roleId}");
        using var response = await SendWithRateLimitRetryAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveRoleAsync(
        string discordUserId,
        string roleId,
        CancellationToken ct)
    {
        using var request = BotRequest(
            HttpMethod.Delete,
            $"guilds/{_options.GuildId}/members/{discordUserId}/roles/{roleId}");
        using var response = await SendWithRateLimitRetryAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemoveMemberAsync(
        string discordUserId,
        string auditReason,
        CancellationToken ct)
    {
        using var request = BotRequest(
            HttpMethod.Delete,
            $"guilds/{_options.GuildId}/members/{discordUserId}");
        request.Headers.TryAddWithoutValidation(
            "X-Audit-Log-Reason",
            Uri.EscapeDataString(auditReason));
        using var response = await SendWithRateLimitRetryAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    private HttpRequestMessage BotRequest(HttpMethod method, string relativeUrl)
    {
        var request = new HttpRequestMessage(method, relativeUrl);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bot", _options.BotToken);
        return request;
    }

    private async Task<HttpResponseMessage> SendWithRateLimitRetryAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        // En código final conviene crear una petición nueva en cada intento,
        // porque HttpRequestMessage no se puede enviar dos veces.
        var response = await _http.SendAsync(request, ct);
        if ((int)response.StatusCode != 429)
            return response;

        var retryAfter = response.Headers.RetryAfter?.Delta
            ?? TimeSpan.FromSeconds(1);
        _logger.LogWarning(
            "Discord rate limit; retrying after {Delay}.", retryAfter);
        response.Dispose();
        await Task.Delay(retryAfter, ct);
        throw new DiscordRetryableException("Rate limited by Discord.");
    }
}

public sealed class DiscordRetryableException(string message)
    : Exception(message);
```

El ejemplo anterior muestra la forma de las operaciones. En la implementación final el retry debe vivir en un `DelegatingHandler` o política que pueda clonar la petición y respetar `Retry-After`; no se debe intentar reenviar la misma instancia de `HttpRequestMessage`.

### 20.9. Estado OAuth2 de un solo uso

Se puede implementar con `IDistributedCache`. En producción, si existen varias instancias del sitio, la caché debe ser distribuida —por ejemplo SQL Server o Redis— y no memoria local.

```csharp
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;

namespace SNEStorage.Discord;

public sealed class DiscordOAuthStateStore(IDistributedCache cache)
{
    private const string Prefix = "discord-oauth-state:";

    public async Task<string> CreateAsync(string userId, CancellationToken ct)
    {
        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        var value = JsonSerializer.Serialize(new OAuthState(userId));
        await cache.SetStringAsync(
            Prefix + state,
            value,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            },
            ct);
        return state;
    }

    public async Task<string?> TakeUserIdAsync(
        string state,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(state))
            return null;

        var key = Prefix + state;
        var json = await cache.GetStringAsync(key, ct);
        if (json is null)
            return null;

        // Borrar antes de continuar impide reutilizar el callback.
        await cache.RemoveAsync(key, ct);
        return JsonSerializer.Deserialize<OAuthState>(json)?.UserId;
    }

    private sealed record OAuthState(string UserId);
}
```

### 20.10. Servicio de vinculación

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SNEStorage.Models;

namespace SNEStorage.Discord;

public enum DiscordLinkResult
{
    Success,
    InvalidState,
    UserNotFound,
    EmailNotConfirmed,
    UserBanned,
    DiscordAccountAlreadyLinked,
    DiscordBotNotAllowed,
    JoinFailed
}

public sealed class DiscordLinkService(
    SnestorageContext db,
    UserManager<ApplicationUser> userManager,
    DiscordApiClient discord,
    DiscordOAuthStateStore stateStore,
    IOptions<DiscordOptions> options)
{
    private readonly DiscordOptions _options = options.Value;

    public async Task<Uri> CreateAuthorizationUriAsync(
        string userId,
        CancellationToken ct)
    {
        var state = await stateStore.CreateAsync(userId, ct);
        var url = QueryHelpers.AddQueryString(
            "https://discord.com/oauth2/authorize",
            new Dictionary<string, string?>
            {
                ["response_type"] = "code",
                ["client_id"] = _options.ApplicationId,
                ["scope"] = "identify guilds.join",
                ["state"] = state,
                ["redirect_uri"] = _options.CallbackUri.ToString(),
                ["prompt"] = "consent"
            });
        return new Uri(url);
    }

    public async Task<DiscordLinkResult> CompleteAsync(
        string code,
        string state,
        CancellationToken ct)
    {
        var userId = await stateStore.TakeUserIdAsync(state, ct);
        if (userId is null)
            return DiscordLinkResult.InvalidState;

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
            return DiscordLinkResult.UserNotFound;
        if (!await userManager.IsEmailConfirmedAsync(user))
            return DiscordLinkResult.EmailNotConfirmed;

        var now = DateTime.UtcNow;
        var banned = await db.Bans.AnyAsync(
            b => b.UserId == userId &&
                 (b.IsPermanent || b.ExpireDate > now),
            ct);
        if (banned)
            return DiscordLinkResult.UserBanned;

        var token = await discord.ExchangeCodeAsync(code, ct);
        var discordUser = await discord.GetCurrentUserAsync(token.AccessToken, ct);
        if (discordUser.Bot)
            return DiscordLinkResult.DiscordBotNotAllowed;

        var belongsToAnotherUser = await db.DiscordAccountLinks.AnyAsync(
            l => l.DiscordUserId == discordUser.Id && l.UserId != userId,
            ct);
        if (belongsToAnotherUser)
            return DiscordLinkResult.DiscordAccountAlreadyLinked;

        var link = await db.DiscordAccountLinks
            .SingleOrDefaultAsync(l => l.UserId == userId, ct);

        if (link is null)
        {
            link = new DiscordAccountLink
            {
                UserId = userId,
                DiscordUserId = discordUser.Id,
                DiscordUsername = discordUser.Username,
                DiscordGlobalName = discordUser.GlobalName,
                LinkedAtUtc = now,
                MembershipState = DiscordMembershipState.LinkedNotJoined
            };
            db.DiscordAccountLinks.Add(link);
        }
        else
        {
            link.DiscordUserId = discordUser.Id;
            link.DiscordUsername = discordUser.Username;
            link.DiscordGlobalName = discordUser.GlobalName;
            link.LinkedAtUtc = now;
            link.MembershipState = DiscordMembershipState.LinkedNotJoined;
        }

        // La restricción única de la base de datos sigue siendo la defensa
        // definitiva frente a dos callbacks concurrentes.
        await db.SaveChangesAsync(ct);

        try
        {
            await discord.AddGuildMemberAsync(
                discordUser.Id,
                token.AccessToken,
                ct);

            link.MembershipState = DiscordMembershipState.Joined;
            link.LastVerifiedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return DiscordLinkResult.Success;
        }
        catch (HttpRequestException)
        {
            link.MembershipState = DiscordMembershipState.LinkedNotJoined;
            await db.SaveChangesAsync(ct);
            return DiscordLinkResult.JoinFailed;
        }
    }
}
```

Después del alta exitosa se debe llamar a `DiscordMembershipService.EnsureRolesAsync`. Se mantiene separado en el ejemplo para no convertir `DiscordLinkService` en una clase excesivamente grande.

### 20.11. Controlador OAuth2

Archivo propuesto: `SNEStorage/Controllers/DiscordController.cs`.

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SNEStorage.Discord;
using SNEStorage.Models;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/discord")]
public sealed class DiscordController(
    DiscordLinkService linkService,
    SnestorageContext db) : ControllerBase
{
    [Authorize]
    [HttpGet("link/status")]
    public async Task<ActionResult<DiscordLinkStatusDto>> Status(
        CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var link = await db.DiscordAccountLinks
            .AsNoTracking()
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);

        return Ok(link is null
            ? new DiscordLinkStatusDto(false, null, null, null)
            : new DiscordLinkStatusDto(
                true,
                link.DiscordUsername,
                link.DiscordGlobalName,
                link.MembershipState.ToString()));
    }

    [Authorize]
    [HttpGet("link/start")]
    public async Task<IActionResult> Start(CancellationToken ct)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId is null)
            return Unauthorized();

        var uri = await linkService.CreateAuthorizationUriAsync(userId, ct);
        return Redirect(uri.ToString());
    }

    // El callback puede llegar sin el JWT de la SPA. La identidad se obtiene
    // del state de un solo uso, no de parámetros proporcionados por el usuario.
    [AllowAnonymous]
    [HttpGet("link/callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string code,
        [FromQuery] string state,
        CancellationToken ct)
    {
        var result = await linkService.CompleteAsync(code, state, ct);
        return result switch
        {
            DiscordLinkResult.Success =>
                Redirect("/Profile/Edit?discord=linked"),
            DiscordLinkResult.JoinFailed =>
                Redirect("/Profile/Edit?discord=retry"),
            DiscordLinkResult.DiscordAccountAlreadyLinked =>
                Conflict("That Discord account is already linked."),
            DiscordLinkResult.InvalidState => BadRequest("Invalid OAuth state."),
            _ => BadRequest("Discord account could not be linked.")
        };
    }
}

public sealed record DiscordLinkStatusDto(
    bool Linked,
    string? Username,
    string? GlobalName,
    string? MembershipState);
```

Para la desvinculación se agrega un `DELETE /api/discord/link` autenticado que crea `discord.membership_revoked`, borra o invalida el vínculo y deja que el worker retire al miembro. Si se borra la fila inmediatamente, el evento debe conservar el `DiscordUserId` dentro de `PayloadJson`.

### 20.12. Sincronización de color

Normalizador:

```csharp
using System.Text.RegularExpressions;

namespace SNEStorage.Discord;

public static partial class DiscordColor
{
    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColorRegex();

    public static string Normalize(string value)
    {
        var trimmed = value.Trim();
        if (!HexColorRegex().IsMatch(trimmed))
            throw new ArgumentException("Color must use #RRGGBB.", nameof(value));
        return trimmed.ToUpperInvariant();
    }

    public static int ToInteger(string normalized) =>
        Convert.ToInt32(normalized[1..], 16);
}
```

Servicio propuesto, dejando las llamadas REST de roles dentro de `DiscordApiClient`:

```csharp
public sealed class DiscordColorRoleService(
    SnestorageContext db,
    DiscordApiClient discord,
    IOptions<DiscordOptions> options)
{
    private readonly DiscordOptions _options = options.Value;

    public async Task SyncAsync(string userId, CancellationToken ct)
    {
        var data = await db.DiscordAccountLinks
            .Include(x => x.User)
            .ThenInclude(x => x!.UserInfo)
            .SingleOrDefaultAsync(x => x.UserId == userId, ct);

        var rawColor = data?.User?.UserInfo?.UsernameColor;
        if (data is null || string.IsNullOrWhiteSpace(rawColor))
            return;

        var color = DiscordColor.Normalize(rawColor);
        var role = await db.DiscordColorRoles
            .SingleOrDefaultAsync(x => x.ColorHex == color, ct);

        if (role is null)
        {
            var count = await db.DiscordColorRoles.CountAsync(ct);
            if (count >= _options.MaxManagedColorRoles)
                throw new InvalidOperationException(
                    "Discord color role budget exhausted.");

            var created = await discord.CreateColorRoleAsync(
                $"color-{color[1..].ToLowerInvariant()}",
                DiscordColor.ToInteger(color),
                ct);

            role = new DiscordColorRole
            {
                ColorHex = color,
                DiscordRoleId = created.Id,
                LastUsedAtUtc = DateTime.UtcNow
            };
            db.DiscordColorRoles.Add(role);
            await db.SaveChangesAsync(ct);
        }

        // Primero se agrega el nuevo para evitar una ventana sin color.
        await discord.AddRoleAsync(
            data.DiscordUserId,
            role.DiscordRoleId,
            ct);

        if (!string.IsNullOrWhiteSpace(data.LastColorRoleId) &&
            data.LastColorRoleId != role.DiscordRoleId)
        {
            await discord.RemoveRoleAsync(
                data.DiscordUserId,
                data.LastColorRoleId,
                ct);
        }

        data.LastColorRoleId = role.DiscordRoleId;
        data.LastVerifiedAtUtc = DateTime.UtcNow;
        role.LastUsedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }
}
```

`CreateColorRoleAsync` debe crear el rol sin permisos, `hoist=false` y `mentionable=false`, y luego colocarlo en la zona administrada por el bot.

### 20.13. Cambio concreto en `AccountService.EditUserInfo`

Antes de asignar `UsernameColor`, capturar el color actual. El evento se agrega al mismo contexto antes de `SaveChangesAsync`.

```csharp
var previousColor = string.IsNullOrWhiteSpace(info.UsernameColor)
    ? null
    : DiscordColor.Normalize(info.UsernameColor);

var nextColor = dto.UsernameColor is null
    ? previousColor
    : DiscordColor.Normalize(dto.UsernameColor);

info.UsernameColor = nextColor ?? info.UsernameColor;

if (nextColor is not null && nextColor != previousColor)
{
    var hasDiscordLink = await _context.DiscordAccountLinks
        .AnyAsync(link => link.UserId == userId, ct);

    if (hasDiscordLink)
    {
        _context.IntegrationOutboxEvents.Add(
            IntegrationOutboxEvent.ForProfileColor(userId, nextColor));
    }
}

// El SaveChangesAsync existente guarda perfil y evento juntos.
await _context.SaveChangesAsync(ct);
```

No se debe llamar a la API de Discord dentro de `EditUserInfo`.

### 20.14. Cambios concretos en creación de recursos

En `ResourceService.Create`, inmediatamente después de `_context.Resources.Add(res)`:

```csharp
_context.Resources.Add(res);
_context.IntegrationOutboxEvents.Add(
    IntegrationOutboxEvent.ForResource(res));

return await _dbHelperService.TrySyncContextWithFiles(
    batchfile,
    res,
    ResourceServiceResult.DBError,
    ct);
```

Como el evento referencia la entidad `res`, Entity Framework resuelve el `ResourceId` generado dentro del mismo `SaveChangesAsync`.

En `ResourceService.AddVersion`, después de agregar la versión:

```csharp
_context.ResourceVersions.Add(version);
_context.IntegrationOutboxEvents.Add(
    IntegrationOutboxEvent.ForVersion(version));

var result = await _dbHelperService.TryFunction(
    async cancel => (int?)await _context.SaveChangesAsync(cancel),
    ct: ct);
```

El worker vuelve a consultar el recurso. No es necesario serializar toda la descripción o las relaciones dentro del evento.

### 20.15. Procesador de eventos y publicación

```csharp
using Microsoft.EntityFrameworkCore;

namespace SNEStorage.Discord;

public sealed class DiscordOutboxProcessor(
    SnestorageContext db,
    DiscordColorRoleService colors,
    DiscordResourcePublisher resources,
    DiscordMembershipService memberships)
{
    public async Task ProcessAsync(
        IntegrationOutboxEvent item,
        CancellationToken ct)
    {
        switch (item.EventType)
        {
            case IntegrationEventTypes.ProfileColorChanged:
                await colors.SyncAsync(item.UserId!, ct);
                break;

            case IntegrationEventTypes.ResourceCreated:
                item.DiscordMessageId = await resources
                    .PublishCreatedAsync(item.ResourceId!.Value, item.Id, ct);
                break;

            case IntegrationEventTypes.ResourceVersionCreated:
                item.DiscordMessageId = await resources
                    .PublishVersionAsync(
                        item.ResourceVersionId!.Value,
                        item.Id,
                        ct);
                break;

            case IntegrationEventTypes.MembershipRevoked:
                await memberships.RemoveFromPayloadAsync(item.PayloadJson, ct);
                break;

            default:
                throw new InvalidOperationException(
                    $"Unknown integration event type: {item.EventType}");
        }
    }
}
```

Worker básico:

```csharp
public sealed class DiscordOutboxWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<DiscordOutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider
                .GetRequiredService<SnestorageContext>();
            var processor = scope.ServiceProvider
                .GetRequiredService<DiscordOutboxProcessor>();

            var now = DateTime.UtcNow;
            var items = await db.IntegrationOutboxEvents
                .Where(x => x.ProcessedAtUtc == null &&
                            x.AvailableAtUtc <= now &&
                            (x.LockedUntilUtc == null || x.LockedUntilUtc < now))
                .OrderBy(x => x.CreatedAtUtc)
                .Take(20)
                .ToListAsync(stoppingToken);

            foreach (var item in items)
            {
                item.LockedUntilUtc = now.AddMinutes(2);
                await db.SaveChangesAsync(stoppingToken);

                try
                {
                    await processor.ProcessAsync(item, stoppingToken);
                    item.ProcessedAtUtc = DateTime.UtcNow;
                    item.LockedUntilUtc = null;
                    item.LastError = null;
                }
                catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                {
                    item.AttemptCount++;
                    item.LockedUntilUtc = null;
                    item.LastError = ex.Message[..Math.Min(ex.Message.Length, 2000)];
                    var seconds = Math.Min(3600, Math.Pow(2, item.AttemptCount));
                    item.AvailableAtUtc = DateTime.UtcNow
                        .AddSeconds(seconds + Random.Shared.Next(0, 10));
                    logger.LogError(
                        ex,
                        "Discord outbox event {EventId} failed.",
                        item.Id);
                }

                await db.SaveChangesAsync(stoppingToken);
            }
        }
    }
}
```

Ese worker es suficiente con una sola instancia de la aplicación. Si producción ejecuta varias instancias, la adquisición del lote debe reemplazarse por un lease atómico de SQL Server usando una transacción con `UPDLOCK` y `READPAST`, o por una cola externa. Leer y luego escribir `LockedUntilUtc` no evita por sí solo una carrera entre dos procesos.

### 20.16. Publicador de recursos

```csharp
public sealed class DiscordResourcePublisher(
    SnestorageContext db,
    DiscordApiClient discord,
    DiscordManagedResourceResolver resources,
    IOptions<DiscordOptions> options)
{
    private readonly DiscordOptions _options = options.Value;

    public async Task<string?> PublishCreatedAsync(
        long resourceId,
        Guid eventId,
        CancellationToken ct)
    {
        var resource = await db.Resources
            .AsNoTracking()
            .Include(x => x.Videogame)
            .Include(x => x.ResourceResourceTypes)
                .ThenInclude(x => x.ResourceType)
            .Include(x => x.ResourcesAuthors)
                .ThenInclude(x => x.User)
            .SingleOrDefaultAsync(x => x.Id == resourceId, ct);

        // En el código actual VisibilityId == 1 es la consulta pública.
        if (resource is null || resource.VisibilityId != 1)
            return null;

        var channelId = await resources.ResolveIdAsync(
            "Channel",
            _options.ResourceLogChannelKey,
            ct);

        var url = $"{_options.PublicBaseUrl.TrimEnd('/')}/Resource/{resource.Id}";
        var description = resource.Description.Length <= 1000
            ? resource.Description
            : resource.Description[..997] + "...";

        var message = new
        {
            embeds = new[]
            {
                new
                {
                    title = resource.Name,
                    url,
                    description,
                    fields = new[]
                    {
                        new
                        {
                            name = "Game",
                            value = resource.Videogame?.Name ?? "Unknown",
                            inline = true
                        }
                    },
                    footer = new
                    {
                        text = $"SNEStorage event {eventId:N}"
                    }
                }
            },
            allowed_mentions = new
            {
                parse = Array.Empty<string>()
            }
        };

        return await discord.CreateMessageAsync(channelId, message, ct);
    }

    public Task<string?> PublishVersionAsync(
        long versionId,
        Guid eventId,
        CancellationToken ct)
    {
        // Misma idea: cargar versión + recurso, comprobar visibilidad y
        // publicar un embed con número de versión, changelog y enlace.
        throw new NotImplementedException();
    }
}
```

El footer con `eventId` permite que, antes de reintentar una publicación cuyo resultado sea incierto, el worker busque ese identificador entre los mensajes recientes del bot. Discord no ofrece una clave de idempotencia al crear un mensaje; sin esa comprobación queda una pequeña ventana de duplicación si el proceso muere después de recibir éxito y antes de guardar `DiscordMessageId`.

### 20.17. Control de entradas mediante Gateway

Para recibir `Guild Member Add` se puede usar una biblioteca Gateway madura como Discord.Net. La versión debe fijarse y validarse al momento de implementar.

Referencia de paquete en `SNEStorage.csproj`:

```xml
<PackageReference Include="Discord.Net.WebSocket" Version="VERSION_VALIDADA" />
```

Worker propuesto:

```csharp
using Discord;
using Discord.WebSocket;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace SNEStorage.Discord;

public sealed class DiscordGatewayWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<DiscordOptions> options,
    ILogger<DiscordGatewayWorker> logger) : BackgroundService
{
    private readonly DiscordOptions _options = options.Value;
    private DiscordSocketClient? _client;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _client = new DiscordSocketClient(new DiscordSocketConfig
        {
            GatewayIntents = GatewayIntents.Guilds |
                             GatewayIntents.GuildMembers
        });

        _client.Log += message =>
        {
            logger.LogInformation("Discord Gateway: {Message}", message.Message);
            return Task.CompletedTask;
        };

        _client.UserJoined += HandleUserJoinedAsync;

        await _client.LoginAsync(TokenType.Bot, _options.BotToken);
        await _client.StartAsync();

        try
        {
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            await _client.StopAsync();
            await _client.LogoutAsync();
        }
    }

    private async Task HandleUserJoinedAsync(SocketGuildUser member)
    {
        if (member.Guild.Id.ToString() != _options.GuildId)
            return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<SnestorageContext>();
        var memberships = scope.ServiceProvider
            .GetRequiredService<DiscordMembershipService>();

        if (member.IsBot &&
            !_options.AllowedBotUserIds.Contains(member.Id.ToString()))
        {
            await memberships.RemoveAsync(
                member.Id.ToString(),
                "Bot is not in the SNEStorage allowlist.",
                CancellationToken.None);
            return;
        }

        var link = await db.DiscordAccountLinks
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.DiscordUserId == member.Id.ToString());

        if (link is not null)
        {
            await memberships.EnsureRolesAsync(
                link.UserId,
                CancellationToken.None);
            return;
        }

        await memberships.QuarantineThenRemoveAsync(
            member.Id.ToString(),
            TimeSpan.FromSeconds(_options.UnauthorizedJoinGraceSeconds),
            CancellationToken.None);
    }
}
```

Los callbacks de Discord.Net no reciben el token de apagado del host. La implementación final debe mantener un token interno enlazado con `stoppingToken`, limitar concurrencia y capturar excepciones para que un evento defectuoso no desconecte el Gateway.

### 20.18. Provisionador del JSON

El provisionador debe trabajar por claves lógicas y nunca solamente por nombres.

```csharp
public sealed class DiscordServerProvisioner(
    SnestorageContext db,
    DiscordApiClient discord,
    IOptions<DiscordOptions> options)
{
    private readonly DiscordOptions _options = options.Value;

    public async Task<ProvisioningReport> ApplyAsync(
        bool dryRun,
        CancellationToken ct)
    {
        var json = await File.ReadAllTextAsync(
            _options.ServerConfigPath,
            ct);
        var config = JsonSerializer.Deserialize<DiscordServerConfig>(
            json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Invalid Discord config.");

        Validate(config);
        var report = new ProvisioningReport();

        // 1. Roles funcionales y rol de cuarentena.
        foreach (var role in config.Roles)
            await UpsertRoleAsync(config.SchemaVersion, role, dryRun, report, ct);

        // 2. Categorías, porque los canales dependen de sus IDs.
        foreach (var category in config.Categories)
            await UpsertCategoryAsync(
                config.SchemaVersion,
                category,
                dryRun,
                report,
                ct);

        // 3. Canales y overwrites de permisos.
        foreach (var category in config.Categories)
            foreach (var channel in category.Channels)
                await UpsertChannelAsync(
                    config.SchemaVersion,
                    category,
                    channel,
                    dryRun,
                    report,
                    ct);

        // 4. AutoMod, reglas, canales especiales y posiciones finales.
        await ApplySafetySettingsAsync(config, dryRun, report, ct);

        if (!dryRun)
            await db.SaveChangesAsync(ct);

        return report;
    }

    private async Task UpsertRoleAsync(
        int schemaVersion,
        DiscordRoleConfig role,
        bool dryRun,
        ProvisioningReport report,
        CancellationToken ct)
    {
        var mapping = await db.DiscordManagedResources.SingleOrDefaultAsync(
            x => x.GuildId == _options.GuildId &&
                 x.ResourceType == "Role" &&
                 x.ResourceKey == role.Key,
            ct);

        if (mapping is null)
        {
            report.AddCreate("Role", role.Key);
            if (dryRun)
                return;

            var created = await discord.CreateRoleAsync(role, ct);
            db.DiscordManagedResources.Add(new DiscordManagedResource
            {
                GuildId = _options.GuildId,
                ResourceType = "Role",
                ResourceKey = role.Key,
                DiscordResourceId = created.Id,
                ConfigSchemaVersion = schemaVersion,
                UpdatedAtUtc = DateTime.UtcNow
            });
            return;
        }

        report.AddUpdate("Role", role.Key);
        if (!dryRun)
        {
            await discord.UpdateRoleAsync(mapping.DiscordResourceId, role, ct);
            mapping.ConfigSchemaVersion = schemaVersion;
            mapping.UpdatedAtUtc = DateTime.UtcNow;
        }
    }
}
```

Faltan en el fragmento los DTO del JSON y las operaciones análogas para categorías, canales y AutoMod, pero el patrón de persistir `ResourceKey → DiscordResourceId` debe mantenerse en todas ellas.

### 20.19. Registro en `Program.cs`

Agregar después del registro de servicios existentes:

```csharp
using SNEStorage.Discord;

services.AddOptions<DiscordOptions>()
    .Bind(builder.Configuration.GetSection(DiscordOptions.SectionName))
    .Validate(
        o => !o.Enabled || !string.IsNullOrWhiteSpace(o.ApplicationId),
        "Discord ApplicationId is required when Discord is enabled.")
    .Validate(
        o => !o.Enabled || !string.IsNullOrWhiteSpace(o.GuildId),
        "Discord GuildId is required when Discord is enabled.")
    .Validate(
        o => !o.Enabled || !string.IsNullOrWhiteSpace(o.BotToken),
        "Discord BotToken is required when Discord is enabled.")
    .Validate(
        o => !o.Enabled || !string.IsNullOrWhiteSpace(o.ClientSecret),
        "Discord ClientSecret is required when Discord is enabled.")
    .ValidateOnStart();

services.AddHttpClient<DiscordApiClient>(client =>
{
    client.BaseAddress = new Uri("https://discord.com/api/v10/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

services.AddScoped<DiscordOAuthStateStore>();
services.AddScoped<DiscordLinkService>();
services.AddScoped<DiscordMembershipService>();
services.AddScoped<DiscordColorRoleService>();
services.AddScoped<DiscordOutboxProcessor>();
services.AddScoped<DiscordResourcePublisher>();
services.AddScoped<DiscordManagedResourceResolver>();
services.AddScoped<DiscordServerProvisioner>();

if (builder.Configuration.GetValue<bool>("Discord:Enabled"))
{
    services.AddHostedService<DiscordOutboxWorker>();
    services.AddHostedService<DiscordGatewayWorker>();
    services.AddHostedService<DiscordReconciliationWorker>();
}
```

El proyecto ya registra `AddDistributedMemoryCache`. Eso funciona para desarrollo con una sola instancia. Para varias instancias debe sustituirse por un proveedor distribuido real antes de confiar en el `state` OAuth2.

### 20.20. Comando de setup

Una forma sencilla es reconocer los argumentos antes de `app.Run()`:

```csharp
if (args.Length >= 2 &&
    args[0].Equals("discord", StringComparison.OrdinalIgnoreCase) &&
    args[1].Equals("setup", StringComparison.OrdinalIgnoreCase))
{
    var dryRun = args.Contains("--dry-run", StringComparer.OrdinalIgnoreCase);
    await using var scope = app.Services.CreateAsyncScope();
    var provisioner = scope.ServiceProvider
        .GetRequiredService<DiscordServerProvisioner>();
    var report = await provisioner.ApplyAsync(dryRun, CancellationToken.None);
    Console.WriteLine(report.ToText());
    return;
}

app.Run();
```

Para evitar que los hosted services se conecten mientras se ejecuta setup, la condición de registro debe excluir el modo de comando:

```csharp
var isDiscordSetup = args.Length >= 2 &&
    args[0].Equals("discord", StringComparison.OrdinalIgnoreCase) &&
    args[1].Equals("setup", StringComparison.OrdinalIgnoreCase);

if (!isDiscordSetup &&
    builder.Configuration.GetValue<bool>("Discord:Enabled"))
{
    services.AddHostedService<DiscordOutboxWorker>();
    services.AddHostedService<DiscordGatewayWorker>();
    services.AddHostedService<DiscordReconciliationWorker>();
}
```

## 21. Paso a paso de implementación

### Paso 1: crear un servidor de Discord de desarrollo

No probar primero contra el servidor real.

1. Crear un guild privado llamado, por ejemplo, `SNEStorage Dev`.
2. Instalar allí la misma aplicación o una aplicación Discord separada de desarrollo.
3. Copiar su `Guild ID` con Developer Mode activado.
4. Mantener `Discord:Enabled=false` en producción hasta completar las pruebas.

Verificación: el bot aparece en el guild de desarrollo, todavía sin ejecutar ninguna automatización.

### Paso 2: configurar la aplicación en Discord Developer Portal

1. En OAuth2, registrar exactamente la redirect URI local:

   ```text
   https://localhost:7250/api/discord/link/callback
   ```

2. Registrar también la URI de producción cuando esté disponible:

   ```text
   https://snestorage.dev/api/discord/link/callback
   ```

3. En Bot, activar `Server Members Intent` (`GUILD_MEMBERS`).
4. Desactivar `Public Bot` si nadie más debe instalarlo.
5. No activar `Message Content Intent`; esta propuesta no lee mensajes.
6. Colocar el rol del bot por encima de todos los roles que administrará.

Verificación: las redirect URIs coinciden carácter por carácter y el intent de miembros aparece habilitado.

### Paso 3: decidir las cuatro políticas pendientes

Antes de crear la migración, definir:

1. Color exacto o paleta limitada.
2. Expulsión inmediata o cuarentena de 60 segundos.
3. Anuncio al crear o después de moderación.
4. Expulsión al desvincular —recomendada— o conservación sin permisos.

Verificación: estas decisiones quedan reflejadas en `server-config.json` y en `DiscordOptions`.

### Paso 4: agregar la dependencia Gateway

Desde la raíz del repositorio:

```powershell
dotnet add SNEStorage/SNEStorage.csproj package Discord.Net.WebSocket
```

Después, reemplazar la versión flotante elegida por una versión fija validada en el `.csproj`.

Verificación:

```powershell
dotnet restore SNEStorage.sln
dotnet build SNEStorage/SNEStorage.csproj --no-restore
```

### Paso 5: crear modelos y mapeos

1. Crear `DiscordAccountLink`.
2. Crear `IntegrationOutboxEvent`.
3. Crear `DiscordManagedResource`.
4. Crear `DiscordColorRole`.
5. Agregar la navegación a `ApplicationUser`.
6. Crear los cuatro archivos `Build`.
7. Registrar los cuatro `DbSet` y mapeos en `SnestorageContext`.

Verificación: el proyecto compila antes de generar la migración.

### Paso 6: generar y revisar la migración

```powershell
dotnet ef migrations add AddDiscordIntegration --project SNEStorage
```

Revisar manualmente que contenga:

- índice único de `discord_user_id`;
- relación uno a uno con `AspNetUsers`;
- índices del outbox;
- longitudes de snowflakes como `varchar(20)`;
- comportamiento `SetNull` o `Cascade` esperado;
- índices filtrados válidos para SQL Server.

Aplicar solamente en la base de desarrollo:

```powershell
dotnet ef database update --project SNEStorage
```

Verificación: intentar insertar dos filas con el mismo `DiscordUserId` debe fallar por la restricción única.

### Paso 7: agregar configuración sin secretos

Agregar la sección `Discord` en `appsettings.Development.json` con IDs y rutas, dejando secretos fuera del archivo.

Guardar secretos locales:

```powershell
dotnet user-secrets set "Discord:BotToken" "TOKEN" --project SNEStorage
dotnet user-secrets set "Discord:ClientSecret" "SECRET" --project SNEStorage
```

Verificación: `git diff` no muestra el token ni el client secret.

### Paso 8: implementar y probar `DiscordApiClient`

Implementar primero operaciones aisladas:

1. Obtener el usuario de OAuth2.
2. Agregar miembro.
3. Obtener roles.
4. Crear rol.
5. Asignar y retirar rol.
6. Crear mensaje.
7. Retirar miembro.

Usar una interfaz o un `HttpMessageHandler` falso en pruebas para comprobar `401`, `403`, `429` y `5xx` sin golpear Discord.

Verificación: ningún log contiene `Authorization`, access token, bot token o client secret.

### Paso 9: implementar OAuth2

1. Crear `DiscordOAuthStateStore`.
2. Crear `DiscordLinkService`.
3. Crear `DiscordController`.
4. Agregar en la edición de perfil el botón “Vincular Discord”.
5. Mostrar estados `linked`, `retry`, `conflict` y `banned`.
6. Implementar desvinculación autenticada.

Pruebas manuales:

1. Iniciar sesión en SNEStorage.
2. Pulsar vincular.
3. Autorizar Discord.
4. Confirmar una fila en `discord_account_link`.
5. Confirmar que el usuario entra al guild.
6. Reutilizar la URL del callback: debe ser rechazada porque `state` ya fue consumido.
7. Intentar vincular el mismo Discord desde otra cuenta: debe devolver conflicto.

### Paso 10: implementar roles base y membresía

1. Crear `DiscordManagedResourceResolver`.
2. Crear `DiscordMembershipService`.
3. Al finalizar OAuth2, asignar `SNEStorage Member`.
4. Implementar el Gateway con `GuildMembers`.
5. Agregar cuarentena y expulsión.
6. Agregar allowlist de bots.
7. Implementar conciliación periódica.

Verificación: entrar con una invitación desde una cuenta no vinculada no concede acceso y termina en expulsión; entrar mediante SNEStorage sí asigna el rol.

### Paso 11: implementar sincronización de color

1. Dejar los roles funcionales sin color si el perfil debe prevalecer.
2. Implementar `DiscordColorRoleService`.
3. Agregar el evento outbox en `AccountService.EditUserInfo`.
4. Implementar el procesador del evento.
5. Probar dos usuarios con el mismo color.
6. Probar el cambio de un color a otro.
7. Probar el límite artificialmente con `MaxManagedColorRoles=2`.

Verificación: solamente queda un rol `color-*` administrado en cada usuario y el color visible coincide con el perfil.

### Paso 12: implementar el outbox

1. Crear `DiscordOutboxProcessor`.
2. Crear `DiscordOutboxWorker`.
3. Implementar backoff y límite de intentos.
4. Si habrá múltiples instancias, implementar lease atómico antes del despliegue.
5. Agregar métricas de pendientes y errores.

Verificación: apagar la conectividad de Discord, crear un evento y comprobar que queda pendiente; restaurar conectividad y comprobar que se procesa.

### Paso 13: conectar creación de recursos

1. Agregar `ForResource(res)` antes del `SaveChangesAsync` existente.
2. Agregar `ForVersion(version)` antes de guardar una versión.
3. Implementar `DiscordResourcePublisher`.
4. Consultar otra vez el recurso desde el worker.
5. Filtrar `VisibilityId != 1`.
6. Decidir y aplicar la condición de moderación.
7. Enviar `allowed_mentions.parse = []`.

Verificación: recurso público produce un mensaje; recurso privado no produce ninguno; reintentar el mismo evento no produce un duplicado detectable.

### Paso 14: implementar el setup del servidor

1. Crear DTOs que representen `server-config.json`.
2. Validar claves repetidas, referencias inexistentes y permisos desconocidos.
3. Implementar roles.
4. Implementar categorías.
5. Implementar canales y permission overwrites.
6. Implementar AutoMod y ajustes nativos soportados.
7. Guardar mappings.
8. Implementar `--dry-run`.

Primera prueba:

```powershell
dotnet run --project SNEStorage -- discord setup --dry-run
```

Aplicación en el guild de desarrollo:

```powershell
dotnet run --project SNEStorage -- discord setup
```

Volver a ejecutar el dry run. Debe informar cero creaciones y solamente cambios reales, si existen.

### Paso 15: ejecutar pruebas automatizadas

Agregar pruebas como mínimo para:

- normalización de colores;
- `state` de un solo uso;
- cuenta Discord duplicada;
- cuenta baneada;
- construcción de enlaces;
- filtrado de recursos privados;
- backoff del outbox;
- idempotencia del provisionador;
- entrada de bot no autorizado;
- pérdida de permisos del bot.

Verificación:

```powershell
dotnet test SNEStorage.sln --no-restore
```

### Paso 16: preparar producción

1. Hacer backup de la base de datos.
2. Aplicar la migración.
3. Configurar secretos en el hosting.
4. Confirmar HTTPS y callback de producción.
5. Ejecutar setup con `--dry-run`.
6. Ejecutar setup una vez.
7. Revisar jerarquía de roles.
8. Activar `Discord:Enabled=true`.
9. Verificar Gateway, outbox y health checks.
10. Probar con una cuenta real no administrativa.

Plan de reversión:

- desactivar `Discord:Enabled` para detener Gateway y workers;
- conservar filas de outbox sin borrarlas;
- no revertir la migración mientras existan datos útiles;
- retirar manualmente permisos sensibles del bot si se sospecha compromiso;
- rotar inmediatamente `BotToken` y `ClientSecret` si aparecen en logs o Git.

### Paso 17: endurecer permisos después del setup

El provisionador necesita más permisos que la operación normal. Después de crear el servidor:

1. Revisar si el bot cotidiano todavía necesita `ManageChannels` y `ManageGuild`.
2. Retirar `CreateInstantInvite` si el ingreso usa `guilds.join`.
3. Retirar `ManageWebhooks` si se publica como bot.
4. Conservar `ManageRoles`, `KickMembers`, `ViewChannel`, `SendMessages`, `EmbedLinks` y `ReadMessageHistory` si se usa la detección de duplicados.
5. Para futuras modificaciones estructurales, conceder temporalmente los permisos de setup y retirarlos al terminar.

Verificación final: el bot puede vincular, asignar roles, sincronizar colores, publicar recursos y retirar miembros, pero no tiene `Administrator`.

