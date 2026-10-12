# Seguridad: seguimiento de la revisión

Fecha: 2026-10-09.

Este documento conserva los pendientes de la conversación. No sustituye una nueva revisión del código: los puntos pendientes reflejan hallazgos anteriores y algunos pueden haber sido corregidos posteriormente. No se modificó backend al preparar este documento.

## Foco actual

1. Eliminación de previews: el usuario indica que está corregida; no se volvió a verificar en esta revisión.
2. Validación del contenido real de archivos: evaluar Soenneker.Libraries.ffprobe para videos y una biblioteca de decodificación para imágenes. En curso.
3. Protección del login: verificados RequireConfirmedEmail, configuración de lockout de Identity y lockoutOnFailure: true. Falta aplicar la política HTTP de rate limiting a ambos endpoints de login.

## Pendientes 4–10

### 4. Límites y validación de entradas

- Validar en backend longitudes de nombres, versiones, descripciones, release notes, comentarios, bio, tags y autores externos.
- Alinear los máximos con los tipos y longitudes reales de las columnas.
- Limitar el número de autores, tipos, tags, dependencias y previews; deduplicar antes de hacer consultas.
- Validar URLs y sus longitudes. La validación del navegador no protege llamadas directas a la API.

### 5. Límites de búsquedas

- Revisar que maxAmount tenga un mínimo y máximo en búsquedas de usuarios, videojuegos, recursos y autores externos.
- Evitar que una búsqueda vacía de autores externos devuelva todos los resultados sin Take.
- El usuario indicó que corregiría estos límites; queda pendiente verificar su estado actual.

### 6. Validación de identificadores y opciones

- Comprobar existencia y validez de TimeZoneId, EmailVisibilityId y VisibilityId.
- No permitir visibilidades exclusivas de recursos como visibilidad del email.
- Validar enums y que los identificadores de relaciones que se eliminan pertenezcan al recurso.
- Las foreign keys no sustituyen estas reglas de negocio.
- Se puede considerar una caché de opciones fijas, si el usuario lo decide; no guardar entidades con seguimiento ni DbContext en un singleton.

### 7. URLs sociales y color del usuario

- Validar URLs en backend, además del frontend: URL absoluta, esquema autorizado, host y ruta de la red social correspondiente.
- Comparar dominios completos; un dominio que solo contenga el nombre de una red no es suficiente.
- Validar UsernameColor como hexadecimal de seis dígitos: \A#[0-9A-Fa-f]{6}\z.
- Preservar el contrato de edición: null significa sin cambios; string vacío significa borrar.

### 8. CSRF en acciones que usan la sesión

- Revisar las acciones que modifican datos y reciben autenticación desde la cookie de sesión que se transforma en JWT.
- Diseñar el suministro de un token antiforgery y su envío como header desde los fetch del frontend.
- Aplicar validación en backend a los endpoints correspondientes. UseAntiforgery por sí solo no añade validación automáticamente a todas las acciones MVC.
- Si se soportan clientes externos con bearer explícito, definir su tratamiento por separado.

### 9. Concurrencia de likes y puntuaciones

- Revisar índices únicos por ScoreId y UserId para evitar votos duplicados por peticiones concurrentes.
- Manejar el conflicto de unicidad y limpiar duplicados existentes antes de añadir restricciones.

### 10. Permisos, privacidad y endurecimiento adicional

- Dependencias de recursos que pasan a privados: decisión de diseño pendiente, aplazada expresamente por el usuario.
- Restricciones de edición y borrado de recursos/versiones con Seal of Quality según las reglas publicadas.
- Vigencia de JWT después de logout, bans o cambios de roles: definir política de invalidación.
- Límites de frecuencia y cuotas de almacenamiento/subidas; captcha no sustituye estos límites.
- Revisar almacenamiento de secretos de correo y JWT. Si un valor real fue compartido o versionado, moverlo a configuración privada y rotarlo.
- Revisar que las políticas de Admin/Moderator comprueben el valor esperado del claim, además de su tipo.

Estos puntos se retomarán cuando el usuario lo solicite; no hay un recordatorio programado.

## Propuesta del punto 3: política de login

Propuesta inicial: 20 solicitudes por minuto por IP, compartidas entre POST /api/login/submit y POST /api/account/login. El límite cuenta solicitudes exitosas y fallidas. Ajustar la cifra según el uso real; usuarios que compartan IP comparten el cupo.

En Program.cs, antes de builder.Build():

```csharp
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;

services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy<string>("login", context =>
        RateLimitPartition.GetSlidingWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new SlidingWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                SegmentsPerWindow = 6,
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});
```

En el pipeline, después de UseRouting y antes del resto de middleware actual:

```csharp
app.UseRouting();
app.UseRateLimiter();
app.UseCors();
```

Esta ubicación corresponde a esta política por IP, que no necesita un usuario autenticado. Una política futura por UserId debe ejecutarse después de UseAuthentication. Si se configura ForwardedHeaders, debe ejecutarse antes del limiter y confiar solo en proxies conocidos.

En ambos controladores, importar Microsoft.AspNetCore.RateLimiting y añadir el atributo al método de login existente:

```csharp
// LoginController.Login
[EnableRateLimiting("login")]
[HttpPost("submit")]
[ValidateAntiForgeryToken]
```

```csharp
// AccountController.Login
[EnableRateLimiting("login")]
[HttpPost("login")]
```

No añadirlo a toda la clase: esta propuesta solo limita el login. La llamada directa de LoginController a AccountController.Login no ejecuta nuevamente el middleware HTTP, así que no consume dos permisos.

Opcional, dentro de AddRateLimiter, para dar una respuesta JSON reconocible y Retry-After cuando el limiter proporcione ese dato:

```csharp
options.OnRejected = async (context, ct) =>
{
    if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
    {
        context.HttpContext.Response.Headers["Retry-After"] =
            Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                .ToString(System.Globalization.CultureInfo.InvariantCulture);
    }

    await context.HttpContext.Response.WriteAsJsonAsync(
        new { error = "LoginRateLimited" }, cancellationToken: ct);
};
```

El status 429 lo establece el middleware. Si el login se envía mediante navegación de formulario, ese JSON aparecerá como página de respuesta; el frontend debe adaptarse para presentar un mensaje conservando el formulario. El callback es opcional y no se ha aplicado al código.

Los contadores viven en memoria por instancia del proceso; se reinician con la aplicación y no se comparten entre varias instancias. No requieren base de datos ni un servidor de caché. En hosting, comprobar que RemoteIpAddress represente al cliente y no un proxy compartido; no confiar directamente en headers de IP enviados por cualquiera.

Fuente: [ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0).

## Nota del punto 2: Soenneker.Libraries.ffprobe

El paquete distribuye el ejecutable de Windows ffprobe.exe y lo copia a Resources bajo el directorio de salida. La ruta documentada es:

```csharp
var executable = Path.Combine(AppContext.BaseDirectory, "Resources", "ffprobe.exe");
```

No incluye una API de validación C# ni administra procesos o interpreta resultados. Se debe ejecutar sobre un archivo temporal local, comprobar ExitCode, leer JSON, validar contenedor/streams y aplicar límites de duración, dimensiones, tiempo de ejecución y concurrencia. Usar ProcessStartInfo.ArgumentList, no una cadena de comandos construida con el nombre del archivo. Comprobar que el ejecutable esté incluido en el resultado de publicación y que MonsterASP permita ejecutarlo: ese permiso no ha sido confirmado.

Un probe válido identifica contenido multimedia, pero no garantiza que todo el archivo pueda decodificarse ni que sea inocuo. Para imágenes y avatares, valorar decodificación y reexportación a un formato permitido. Para una comprobación completa de videos, FFmpeg puede recorrer la decodificación; tiene mayor coste que ffprobe.

Fuente: [Repositorio del paquete](https://github.com/soenneker/soenneker.libraries.ffprobe) y [ffprobe](https://ffmpeg.org/ffprobe.html).
