# CAPTCHA: integración del backend

Propuesta para copiar y adaptar. Este documento no modifica los archivos de backend.

## Correcciones del filtro actual

1. `RequireCaptchaAttribute` pasa `action`, `policy` y `allowFormToken`; el constructor del filtro debe recibir los tres argumentos.
2. Después de `await next()` hay que terminar con `return`. De lo contrario se sustituye el resultado del action por un error, incluso si la operación ya se guardó.
3. Las políticas necesitan un contador del servidor. El enum por sí solo no aplica ningún umbral.

## Contador: `Services/CaptchaFrequencyService.cs`

Este ejemplo permite 5 o 10 intentos sin CAPTCHA en una ventana de 5 minutos por usuario y política. Las operaciones con la misma política comparten el contador. Cuenta intentos, incluidos los que luego fallan en el action. Alcanzado el umbral, las operaciones adicionales requieren un token hasta que se renueven los permisos de la ventana; superar un CAPTCHA permite la operación actual y no reinicia el contador.

Usa el limitador de .NET en memoria del proceso, sin queries ni caché de respuestas. Su estado se pierde al reiniciar y no se comparte entre instancias. Ajusta los 5 minutos a tu criterio.

```csharp
using System.Threading.RateLimiting;
using SNEStorage.Filters.Attributes;

namespace SNEStorage.Services;

public sealed class CaptchaFrequencyService : IDisposable
{
    private readonly PartitionedRateLimiter<(string UserId, CaptchaPolicy Policy)> _limiter =
        PartitionedRateLimiter.Create<(string UserId, CaptchaPolicy Policy),
            (string UserId, CaptchaPolicy Policy)>(key =>
            RateLimitPartition.GetSlidingWindowLimiter(key, partition =>
                new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = partition.Policy switch
                    {
                        CaptchaPolicy.After5TriesInShortTime => 5,
                        CaptchaPolicy.After10TriesInShortTime => 10,
                        _ => throw new ArgumentOutOfRangeException(nameof(partition.Policy))
                    },
                    Window = TimeSpan.FromMinutes(5),
                    SegmentsPerWindow = 10,
                    QueueLimit = 0,
                    AutoReplenishment = true
                }));

    public bool TryAllowWithoutCaptcha(string userId, CaptchaPolicy policy)
    {
        using var lease = _limiter.AttemptAcquire((userId, policy));
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}
```

El contador se adquiere de forma atómica: peticiones concurrentes no pueden pasar todas como si fueran la primera. La ventana se renueva por segmentos de 30 segundos.

## Filtro: `Filters/CaptchaFilter.cs`

```csharp
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SNEStorage.Filters.Attributes;
using SNEStorage.Services;

namespace SNEStorage.Filters;

public class CaptchaFilter(
    CaptchaService captchaService,
    CaptchaFrequencyService frequencyService,
    string expectedAction,
    CaptchaPolicy policy,
    bool allowFormToken) : IAsyncResourceFilter
{
    private readonly CaptchaService _captchaService = captchaService;
    private readonly CaptchaFrequencyService _frequencyService = frequencyService;
    private readonly string _expectedAction = expectedAction;
    private readonly CaptchaPolicy _policy = policy;
    private readonly bool _allowFormToken = allowFormToken;

    public async Task OnResourceExecutionAsync(
        ResourceExecutingContext context,
        ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var ct = context.HttpContext.RequestAborted;
        string? token = request.Headers["X-Captcha-Token"].ToString();

        if (string.IsNullOrWhiteSpace(token) && _allowFormToken && request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(ct);
            token = form["cf-turnstile-response"].ToString();
        }

        var userId = context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier);
        // Las políticas condicionales de este proyecto se aplican a usuarios autenticados.
        // Sin un ID verificable se exige CAPTCHA; no se concede un contador anónimo compartido.
        bool conditional = _policy is CaptchaPolicy.After5TriesInShortTime
            or CaptchaPolicy.After10TriesInShortTime;
        if (string.IsNullOrWhiteSpace(token) && conditional &&
            !string.IsNullOrWhiteSpace(userId) &&
            _frequencyService.TryAllowWithoutCaptcha(userId, _policy))
        {
            await next();
            return;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            context.Result = new BadRequestObjectResult(new
            {
                error = "CaptchaRequired",
                action = _expectedAction
            });
            return;
        }

        var result = await _captchaService.ValidateAsync(token, _expectedAction, ct);
        if (result == CaptchaResult.Valid)
        {
            await next();
            return;
        }

        context.Result = new ObjectResult(new
        {
            error = result == CaptchaResult.Invalid ? "CaptchaInvalid" : "CaptchaUnavailable",
            action = _expectedAction
        })
        {
            StatusCode = result == CaptchaResult.Invalid
                ? StatusCodes.Status400BadRequest
                : StatusCodes.Status503ServiceUnavailable
        };
    }
}
```

## Registro en `Program.cs`

Conserva el `AddHttpClient<CaptchaService>` actual y añade:

```csharp
services.AddSingleton<CaptchaFrequencyService>();
```

No registres el contador como transient ni scoped: perdería los intentos al terminar cada petición. Este singleton no debe inyectar DbContext ni otros servicios scoped.

El atributo actual puede mantenerse, incluyendo `Arguments = [action, policy, allowFormToken]`. El frontend envía `X-Captcha-Token` también para Register, por lo que no necesita cambiar `allowFormToken` a true ni modificar los DTOs.

## Acciones que consume el frontend

| Operación | Acción de Turnstile |
|---|---|
| Registro | `user_register` |
| Crear recurso | `resource_create` |
| Editar recurso | `resource_edit` |
| Nueva versión | `resource_createVersion` |
| Editar versión | `resource_editVersion` |
| Comentar un recurso | `resource_addComment` |
| Calificar un recurso | `resource_addOrEditScore` |
| Responder comentario | `comment_reply` |
| Editar comentario | `comment_edit` |
| Like/dislike/remover voto | `comment_AddEditOrDeleteLike` |

Las acciones se comparan respetando mayúsculas. CAPTCHA no reemplaza autenticación, permisos ni antiforgery. Los filtros MVC se ejecutan en solicitudes HTTP; la llamada interna de RegisterController a AccountController no ejecuta un segundo filtro.

## Contrato de errores y reintentos

- `400 { "error": "CaptchaRequired", "action": "..." }`: el action no se ejecutó; el frontend puede mostrar el desafío y reenviar los mismos datos.
- `400 { "error": "CaptchaInvalid", "action": "..." }`: el action no se ejecutó; se necesita un token nuevo.
- `503 { "error": "CaptchaUnavailable", "action": "..." }`: no se ejecuta el action; el usuario puede reintentar conservando su formulario.
- Nunca devolver estos errores después de haber ejecutado `next()`: el frontend confía en que representan un rechazo anterior a la operación.

En subidas grandes la cabecera permite validar antes de que MVC lea los archivos. Un proxy o middleware que bufferice previamente el cuerpo puede retrasarlo; la configuración del hosting debe revisarse al publicar.

## Desarrollo local

La configuración actual autoriza `snestorage.dev`. Para localhost utiliza claves de prueba de Turnstile y configuración de desarrollo con `APISettings:TurnstileSiteKey`, `APISettings:TurnstileAllowedHostnames` y `Turnstile:SecretKey`. El servicio comprueba también la acción y el hostname: revisa qué devuelve Siteverify con las claves dummy; algunos escenarios de prueba pueden requerir un tratamiento explícito limitado a desarrollo. Mantén las comprobaciones estrictas en producción.

Referencias: [filtros MVC](https://learn.microsoft.com/en-us/aspnet/core/mvc/controllers/filters?view=aspnetcore-10.0), [limitadores de frecuencia](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0), [validación Turnstile](https://developers.cloudflare.com/turnstile/get-started/server-side-validation/).
