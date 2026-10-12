# Antiforgery para el release

## Configuración aplicada en backend

Revisión del 2026-10-11: `Program.cs` ya tiene el filtro global, `HeaderName = "X-CSRF-TOKEN"` y `UseAntiforgery()`. Esta revisión comprueba el código; no se enviaron peticiones de escritura para probarlo.

Revisión posterior del mismo día: también restauraste `.AddJsonOptions(...)` con `ReferenceHandler.IgnoreCycles`. Ese ajuste ya está aplicado; el siguiente bloque se conserva como referencia:

```csharp
services.AddControllers(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
})
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    })
    .AddControllersAsServices();
```

Los imports de `Microsoft.AspNetCore.Mvc` y `System.Text.Json.Serialization` ya están presentes. Las instrucciones siguientes se conservan como referencia de la configuración.

En `SNEStorage/Program.cs`, añade:

```csharp
using Microsoft.AspNetCore.Mvc;
```

Reemplaza el primer `services.AddControllers()` por:

```csharp
services.AddControllers(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
})
```

Conserva la cadena actual `.AddJsonOptions(...).AddControllersAsServices()` que sigue a esa llamada.

Añade también antes de `builder.Build()`:

```csharp
services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});
```

Conserva `AddControllersWithViews()` y `app.UseAntiforgery()`; ya están presentes. No hace falta instalar paquetes ni modificar servicios.

El filtro aplica a POST, PUT, PATCH y DELETE de los controladores. GET, HEAD, OPTIONS y TRACE no requieren este token. Los atributos `ValidateAntiForgeryToken` actuales pueden permanecer. `AllowAnonymous` no desactiva antiforgery.

## Frontend preparado

- `App.razor` renderiza un token oculto mediante `AntiforgeryToken`.
- `antiforgery.js` lo envía en `X-CSRF-TOKEN` solo a la misma dirección de origen.
- Registro, recursos, versiones, ratings, comentarios, perfil, noticias y contraseña usan ese transporte. Login y logout conservan sus formularios con el campo antiforgery.
- Confirmación de email ahora hace el POST desde el navegador, con su cookie y token, manteniendo el token de Identity de la URL.
- No se reintentan escrituras ante un rechazo antiforgery. Los reintentos de CAPTCHA siguen limitados a sus errores específicos.

El token antiforgery es distinto del JWT, del token de confirmación y del CAPTCHA.

## Alcance

El frontend solo envía el token; **la protección queda activada cuando aplicas el filtro en backend**. Un POST/PUT/DELETE sin un token válido debe devolver 400 antes de ejecutar la acción. Un envío legítimo desde la aplicación debe seguir funcionando.

La configuración global también exige antiforgery a clientes de Swagger/Postman y a otras llamadas HTTP a estas acciones. Actualmente los envíos de la web están preparados. Si más adelante publicas una API para clientes con Bearer explícito, define su separación de las acciones que aceptan autenticación mediante la cookie de sesión; no eximas indiscriminadamente los endpoints existentes.

Después de cambiar de cuenta en otra pestaña puede ser necesario recargar la página para generar un token correspondiente a la sesión actual.

Referencia: [Documentación oficial de ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).
