# Login después de publicar o reiniciar

Revisión del código del 12 de octubre de 2026. Propuesta para que apliques los cambios de backend; este documento no modifica su implementación. No se han ejecutado pruebas de reinicio ni de producción.

## Causa actual

- `LoginController.Login` guarda el JWT en `HttpContext.Session`.
- `Program.cs` usa `AddDistributedMemoryCache()` como almacenamiento de sesión. Aunque el nombre diga Distributed, este proveedor guarda los datos en el proceso de la aplicación.
- El middleware recupera ese JWT de la sesión y lo añade a Authorization.
- Al reiniciar el proceso se pierde el JWT almacenado. La cookie de sesión que conserva el navegador contiene un identificador, no el JWT.

Aumentar el timeout de Session no resuelve la pérdida al reiniciar. Persistir las claves de Data Protection por sí solo tampoco recupera el contenido de esa sesión.

Referencia: [Estado de sesión de ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/app-state?view=aspnetcore-10.0).

## Cambio propuesto: conservar el JWT en una cookie HttpOnly

Mantiene la validación JWT, los controladores actuales y la comprobación de SecurityStamp. Los cambios siguientes deben aplicarse juntos, especialmente el borrado de la cookie al cerrar sesión.

### 1. LoginController.Login

Después de comprobar que `response.Value` existe, reemplaza `HttpContext.Session.SetString("Token", response.Value.Token)` por:

```csharp
HttpContext.Response.Cookies.Append("__Host-SNEStorage.Auth", response.Value.Token,
    new CookieOptions
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = new DateTimeOffset(response.Value.ExpireTime)
    });
```

Conserva el `LocalRedirect(destination)` existente. La aplicación debe utilizar HTTPS, incluido localhost. No añadas Domain a esta cookie: el prefijo `__Host-` requiere Secure, Path=/ y ausencia de Domain.

### 2. Program.cs

Reemplaza el middleware que obtiene el token de Session por este, conservando su posición antes de `UseAuthentication()`:

```csharp
// Add the browser JWT before authentication runs.
app.Use(async (context, next) =>
{
    var token = context.Request.Cookies["__Host-SNEStorage.Auth"];
    if (!string.IsNullOrEmpty(token) &&
        !context.Request.Headers.ContainsKey("Authorization"))
        context.Request.Headers.Authorization = "Bearer " + token;

    await next();
});
```

Esto permite seguir usando un Authorization explícito para los clientes de la API. Mantén la validación de antiforgery ya configurada: el navegador envía las cookies automáticamente y las operaciones de escritura necesitan esa protección.

No es necesario retirar AddSession/UseSession para aplicar este cambio. Puedes revisar su eliminación por separado si ya no hay otros usos.

### 3. AccountService.Logout

Después de comprobar que `context` existe, elimina también la nueva cookie:

```csharp
context.Response.Cookies.Delete("__Host-SNEStorage.Auth", new CookieOptions
{
    HttpOnly = true,
    Secure = true,
    SameSite = SameSiteMode.Lax,
    Path = "/"
});

context.Session.Remove("Token");
await _signInManager.SignOutAsync();
return true;
```

Mantén la limpieza de la sesión anterior durante la transición. El cierre de sesión sigue eliminando también la cookie de Identity creada por PasswordSignInAsync.

### 4. Configuración y caducidad

- Conserva la misma JWTKey en el servidor al publicar. Cambiarla invalida los JWT anteriores.
- `BuildToken` actualmente establece 30 minutos de validez. Esta propuesta conserva la sesión a través de un reinicio dentro de ese plazo; no amplía su duración.
- Una sesión ya perdida requerirá iniciar sesión nuevamente para obtener la nueva cookie.
- Si posteriormente conectas la renovación de tokens, la respuesta debe actualizar esta cookie. El endpoint actual de refresh no implementa por sí solo una renovación automática en el navegador.
- HttpOnly impide que JavaScript lea el JWT. El frontend no necesita copiarlo a localStorage ni cambiar sus rutas.

## Nueva preferencia: mantener el login durante mucho tiempo

La propuesta anterior resuelve la pérdida al reiniciar, pero conserva el límite actual de 30 minutos. Para evitar también los cierres por tiempo, la preferencia actual es una sesión persistente de larga duración con renovación automática.

### Por qué se pierde ahora incluso mientras usas la web

- BuildToken tiene una caducidad absoluta de 30 minutos. Cada petición no cambia esa fecha.
- AddSession() usa el IdleTimeout predeterminado de 20 minutos. El uso de un circuito Blazor no implica que se acceda y renueve Session en cada interacción.
- PasswordSignInAsync usa isPersistent: false para la cookie de Identity.
- RefreshToken devuelve otro JWT, pero no actualiza la sesión/cookie y el frontend no lo llama automáticamente. Su existencia no renueva el login por sí sola.

### Comportamiento propuesto conservando la autenticación JWT actual

1. Aplicar los tres cambios de cookie JWT descritos arriba; así el login deja de depender de Session.
2. En BuildToken usar `DateTime.UtcNow.AddDays(90)` para la caducidad. La cookie del login sigue usando ExpireTime, por lo que ambos plazos coinciden.
3. Renovar automáticamente desde OnTokenValidated, después de comprobar correctamente el SecurityStamp: si la autenticación procede de la cookie del navegador y al JWT le quedan menos de 45 días, generar otro JWT mediante BuildToken y reemplazar esa cookie con los mismos atributos y su nueva ExpireTime. Si BuildToken falla, conservar la cookie vigente y permitir reintentar en otra petición; nunca emitir credenciales sin validar al usuario.
4. No renovar la cookie en peticiones que traen su propio Authorization. Para distinguirlas, el middleware que copia la cookie al header debe marcar su procedencia en HttpContext.Items antes de UseAuthentication.
5. No reutilizar el endpoint con CAPTCHA para esta renovación interna. La validación del JWT vigente y del SecurityStamp sigue siendo obligatoria.
6. Mantener coherente la cookie de Identity que también crea tu login: cambiar isPersistent a true y configurar su ExpireTimeSpan a 90 días y SlidingExpiration a true. La persistencia de Data Protection descrita abajo sigue siendo necesaria si el proveedor actual no conserva las claves.

Configuración de la cookie de Identity, después de AddIdentity:

```csharp
services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(90);
    options.SlidingExpiration = true;
});
```

SlidingExpiration renueva la cookie de Identity; **no renueva un JWT**. El punto 3 es necesario para renovar también la credencial usada por tus endpoints Bearer. Cambiar solo isPersistent o solo AddDays no completa el comportamiento.

Con este esquema, una persona que vuelve habitualmente conserva el login sin introducir la contraseña. Tras 90 días sin ninguna petición de renovación, las credenciales expiran. Cerrar sesión, invalidar el SecurityStamp o borrar las cookies también termina el login. Cambiar permisos de staff debe invalidar el stamp como se explica más abajo, especialmente con tokens de larga duración.

Esta es una propuesta de implementación, todavía no aplicada al backend. Una credencial robada puede durar hasta su caducidad o revocación; el plazo largo necesita conservar las comprobaciones de SecurityStamp y la protección de la cookie.

Referencia: [Autenticación mediante cookies de ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0).

## Comprobar Data Protection en MonsterASP

AccountService.Login también crea una cookie de Identity. Además, los tokens de confirmación y recuperación de contraseña dependen de Data Protection. Conviene comprobar que el hosting conserva las mismas claves entre publicaciones y reinicios.

No encontré una configuración explícita en Program.cs; eso no demuestra que el hosting esté perdiendo las claves, porque el proveedor predeterminado puede persistirlas. Si necesitas configurarlo expresamente:

```csharp
using Microsoft.AspNetCore.DataProtection;

services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        builder.Configuration["DataProtection:KeyPath"]!))
    .SetApplicationName("SNEStorage");
```

Configura DataProtection:KeyPath con una carpeta privada y persistente del hosting, fuera de wwwroot y que no sea borrada durante la publicación. Debe existir y permitir escritura a la aplicación. No puedo determinar su ruta real desde el repositorio.

La persistencia explícita en archivos desactiva el cifrado automático de claves en reposo. Protege los permisos de esa carpeta y configura protección con DPAPI o certificado si el hosting lo admite. Si ya tienes un almacén de claves, conserva sus claves al configurar una ubicación nueva: empezar con un almacén vacío puede invalidar cookies y enlaces enviados anteriormente.

Referencia: [Configuración de Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0).

## Otros pendientes, por prioridad

### 1. Invalidar credenciales al retirar permisos de staff

**AccountService.AddRole / RemoveRole, líneas 305 y 319.** Se actualizan los claims guardados, pero el JWT emitido anteriormente conserva los claims originales y el mismo SecurityStamp. Retirar un permiso puede dejarlo operativo hasta que expire ese JWT.

Después de modificar los claims correctamente, actualiza el SecurityStamp y comprueba el resultado:

```csharp
if (!result.Succeeded)
    return false;

var stampResult = await _userManager.UpdateSecurityStampAsync(user);
return stampResult.Succeeded;
```

Tu ValidateJwt ya compara ese stamp con el de la base de datos. El usuario tendrá que autenticarse de nuevo. Si falla la actualización del stamp, el cambio de claims ya pudo guardarse: hay que tratar ese fallo como una revocación incompleta, no anunciar que todas sus sesiones quedaron invalidadas.

### 2. Aplicar las restricciones de Seal of Quality en el backend

**ResourceService.Edit, línea 268; EditVersion, línea 548.** Los controles actuales comprueban que el usuario sea colaborador, pero no impiden editar un recurso o una versión con sello. Edit también permite cambiar VisibilityId (línea 429).

Si vas a otorgar sellos en este release, aplica las reglas ya publicadas: un recurso con una versión sellada debe permanecer público y su edición necesita permiso del staff; una versión sellada también debe protegerse. Un checkbox o un botón oculto en frontend no impide llamar a la API. El guardado debe comprobar el estado actual del sello y la autorización correspondiente.

### 3. Garantizar un voto por usuario y comentario

**LikeUsers.Build y CommentService.AddEditOrDeleteLike, línea 102.** El servicio busca el voto antes de insertarlo, pero falta una restricción única. Dos peticiones simultáneas pueden insertar dos votos para la misma pareja ScoreId/UserId.

Añade en el build:

```csharp
entity.HasIndex(like => new { like.ScoreId, like.UserId }).IsUnique();
```

Antes de aplicar la migración, comprueba si ya existen duplicados y resuélvelos. El servicio debe manejar la colisión de inserción sin reportar éxito para un voto que no se guardó. Este punto protege la integridad de los contadores; su prioridad es menor que la autenticación y los permisos de staff.
