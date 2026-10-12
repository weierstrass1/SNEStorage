# Punto 3: límites para el release

Propuesta del 2026-10-11. Revisión posterior: los cambios están aplicados. `Program.cs` contiene el import, `AddRateLimiter`, `GlobalLimiter`, `OnRejected` y `UseRateLimiter` en el orden propuesto. Los 42 atributos de las acciones coinciden con la propuesta; los tres parámetros de comentarios tienen `StringLength(5000)` y `EditProfileDTO.Bio` tiene `StringLength(2000)`. Los bloques de configuración de este documento quedan como referencia; no debes duplicarlos. Esta comprobación es una revisión de código, no una prueba de los cupos con tráfico real.

Las versiones completas propuestas se conservan como referencia en [rate-limiting-archivos-pendientes.md](rate-limiting-archivos-pendientes.md). Ya no representan tareas pendientes. En esta revisión solo se actualizaron los documentos.

Compilación Release comprobada: 0 errores y 7 advertencias existentes. No se ejecutaron pruebas de carga ni peticiones para agotar los cupos. Al publicar, queda comprobar que `RemoteIpAddress` corresponda a la IP del visitante en la configuración del hosting/proxy.

## 1. Límites iniciales

Estos valores son un punto de partida, no cifras medidas de capacidad del hosting.

| Política | Cupo | Clave | Acciones |
|---|---|---|---|
| `login` | 20/minuto | IP | Ambos logins |
| `register` | 5/10 minutos | IP | Ambos registros |
| `password-email` | 3/15 minutos | IP | Solicitud de recuperación |
| `reads` | 240/minuto | IP | Lecturas y búsquedas de recursos, comentarios, noticias y pickers |
| `comments` | 20/minuto | Usuario | Crear, responder, editar y borrar comentarios |
| `votes` | 60/minuto | Usuario | Likes/dislikes y estrellas |
| `writes` | 30/minuto | Usuario; IP si es anónimo | Noticias, edición de versión, cambio de contraseña y roles |
| `uploads` | 10/10 minutos | Usuario | Crear recurso, añadir versión, editar recurso y editar perfil |

Además, máximo **2 peticiones simultáneas** entre todas las acciones con la política `uploads`, por instancia de la aplicación. Esta política también cubre ediciones de recurso/perfil sin archivo: es una simplificación para empezar, ya que esas acciones pueden recibir archivos.

Una política comparte cupo entre sus acciones para una misma clave. CAPTCHA no sustituye estos límites y sus reintentos HTTP también consumen cupo.

## 2. Program.cs: registro

Imports:

```csharp
using Microsoft.AspNetCore.RateLimiting;
using System.Globalization;
using System.Security.Claims;
using System.Threading.RateLimiting;
```

Antes de `builder.Build()`:

```csharp
services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    (string Name, int Amount, int Minutes)[] limits =
    [
        ("login", 20, 1),
        ("register", 5, 10),
        ("password-email", 3, 15),
        ("reads", 240, 1),
        ("comments", 20, 1),
        ("votes", 60, 1),
        ("writes", 30, 1),
        ("uploads", 10, 10)
    ];

    foreach (var limit in limits)
        options.AddPolicy<string>(limit.Name, context =>
        {
            string? userId = limit.Name is "login" or "register" or "password-email" or "reads" ?
                null : context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            string key = userId != null ? $"user:{userId}" :
                $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

            return RateLimitPartition.GetSlidingWindowLimiter(key, _ =>
                new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = limit.Amount,
                    Window = TimeSpan.FromMinutes(limit.Minutes),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == "uploads" ?
            RateLimitPartition.GetConcurrencyLimiter("uploads", _ =>
                new ConcurrencyLimiterOptions
                {
                    PermitLimit = 2,
                    QueueLimit = 0
                }) :
            RateLimitPartition.GetNoLimiter("other"));

    options.OnRejected = async (context, ct) =>
    {
        var response = context.HttpContext.Response;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            response.Headers["Retry-After"] = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                .ToString(CultureInfo.InvariantCulture);

        // Native form navigation: show the existing Login view instead of raw JSON.
        if (context.HttpContext.Request.Path.Equals(new PathString("/api/login/submit")))
        {
            response.Redirect("/Login?result=rate-limited&returnUrl=" +
                Uri.EscapeDataString(context.HttpContext.Request.Query["returnUrl"].ToString()));
            return;
        }

        await response.WriteAsJsonAsync(new { error = "RateLimited" }, cancellationToken: ct);
    };
});
```

Las APIs rechazadas devuelven HTTP 429. El formulario nativo de login recibe una redirección 302 a Login con un mensaje y su destino conservado; su acción de login no se ejecuta. El frontend ya está preparado para ese mensaje. El frontend que usa fetch ya presenta los 429 y conserva los campos; no reintenta automáticamente las escrituras.

## 3. Program.cs: middleware

Pon el limiter en este orden:

```csharp
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();
```

En este proyecto las acciones seleccionan explícitamente el esquema JWT en `Authorize`. Situarlo después de autorización permite que esa autenticación haya establecido el usuario antes de elegir la clave del cupo. Session y el middleware que añade el JWT siguen antes de estas llamadas; `UseRouting()` también debe estar antes.

## 4. Controladores: atributos en las acciones

Importa `Microsoft.AspNetCore.RateLimiting` en los archivos correspondientes y añade **un solo atributo por acción**, conservando sus atributos actuales:

```csharp
[EnableRateLimiting("login")]
```

| Controlador | Métodos | Política |
|---|---|---|
| LoginController | Login | `login` |
| AccountController | Login | `login` |
| RegisterController | Register | `register` |
| AccountController | Register | `register` |
| AccountController | RequestChangePassword | `password-email` |
| ResourceController | AddComment | `comments` |
| CommentController | Reply, Edit, Delete | `comments` |
| CommentController | AddEditOrDeleteLike | `votes` |
| ResourceController | AddOrEditScore | `votes` |
| ResourceController | CreateResource, CreateVersion, Edit | `uploads` |
| ProfileController | EditMyProfile | `uploads` |
| ResourceController | EditVersion | `writes` |
| NewsController | Create, Edit | `writes` |
| AccountController | ChangePassword, AddRole, RemoveRole | `writes` |
| ResourceController | Todas las acciones HTTP GET, incluida Download | `reads` |
| CommentController | GetUserLike, GetReplies, GetResourceComments | `reads` |
| NewsController | FullGet | `reads` |
| UserController | GetUsersLike | `reads` |
| VideogameController | GetVideogamesLike | `reads` |
| ExternalAuthorController | GetExternalAuthorsLike | `reads` |

Pon los atributos en las acciones indicadas, con su política correspondiente. Una llamada C# directa desde LoginController a AccountController no ejecuta nuevamente el middleware ni consume otro permiso.

## 5. Longitud de comentarios y biografía

Propongo **5.000 caracteres por comentario** y **2.000 para Bio**. Son límites de entrada; no necesitas cambiar las columnas `nvarchar(max)` ni generar una migración.

En `ResourceController.AddComment`, `CommentController.Reply` y `CommentController.Edit`, cambia el parámetro a:

```csharp
[FromForm, StringLength(5000)] string commentText
```

Importa `System.ComponentModel.DataAnnotations`. Con `ApiController`, una longitud inválida produce 400 por validación de modelo antes de ejecutar el método. Los chequeos actuales de texto vacío en los servicios permanecen necesarios.

En `EditProfileDTO`:

```csharp
[StringLength(2000)]
public string? Bio { get; set; }
```

`null` sigue significando sin cambios. El editor ya limitaba Bio a 2.000; los tres campos de comentarios ahora tienen `maxlength="5000"`. La validación del navegador solo ayuda a usar el formulario; la validación del backend impone el límite.

Si otros servicios llaman directamente a esos métodos para escribir comentarios, deben aplicar también el límite allí: los atributos MVC solo validan las peticiones HTTP. Los límites de frecuencia no reemplazan los límites de tamaño HTTP/multipart configurados para las subidas.

## 6. Alcance y hosting

- No requiere paquetes adicionales en este proyecto ASP.NET Core ni cambios de base de datos.
- Usa contadores temporales en memoria por proceso, igual que tu servicio de frecuencia de CAPTCHA. Se reinician con el proceso y no se comparten entre varias instancias.
- Los usuarios detrás de la misma IP comparten los cupos anónimos. Comprueba en MonsterASP que `RemoteIpAddress` represente al cliente; si llega la IP de un proxy, configura forwarded headers únicamente para proxies conocidos, antes de leer esa IP. No uses directamente un `X-Forwarded-For` arbitrario.
- Esta configuración cubre las acciones indicadas, siempre que añadas sus atributos. No limita las conexiones de Blazor ni el ancho de banda de las descargas y no sustituye la protección externa contra DDoS que decidiste tratar después.

Fuente: [Rate limiting oficial de ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0).
