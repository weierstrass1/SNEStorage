# Correo de recuperación de contraseña

Presentación añadida: `PasswordReset.html` y `PasswordReset.txt`. No se modificaron EmailService, AccountService, controladores ni configuración.

## Método propuesto en EmailService

```csharp
public async Task<EmailServiceResult> PasswordReset(
    string username,
    string userId,
    string email,
    string encodedToken,
    CancellationToken ct = default)
```

Se puede partir del método Confirmation actual, conservando su envío por MimeKit/MailKit. Cambiar:

1. Archivo HTML: `EmailTemplates/PasswordReset.html`.
2. Asunto: `Reset your password | SNEStorage`.
3. Ruta del enlace: `/Account/ResetPassword/{userId}?token=...`.
4. Placeholder: `{{PasswordResetUrl}}`.
5. Texto alternativo: usar PasswordReset.txt o texto equivalente de recuperación.

`encodedToken` llega desde AccountService como Base64Url del token generado con GeneratePasswordResetTokenAsync. No generar el token en EmailService, ni codificarlo en Base64Url por segunda vez. Uri.EscapeDataString sí corresponde para insertarlo en la URL.

```csharp
var siteUrl = "https://snestorage.dev/";
var url = $"{siteUrl}Account/ResetPassword/" +
    $"{Uri.EscapeDataString(userId)}?token={Uri.EscapeDataString(encodedToken)}";

html = html
    .Replace("{{PasswordResetUrl}}", System.Net.WebUtility.HtmlEncode(url))
    .Replace("{{Username}}", System.Net.WebUtility.HtmlEncode(username))
    .Replace("{{Year}}", year.ToString())
    .Replace("{{SiteUrl}}", System.Net.WebUtility.HtmlEncode(siteUrl));
```

En este bloque `html` es la plantilla ya leída y `year` el año ya obtenido, como en Confirmation. En PasswordReset.txt sustituir los mismos placeholders con los valores originales, sin HtmlEncode.

El logo conserva `cid:snestorage-logo`: reutilizar el recurso `EmailTemplates/assets/caja.png` con ContentId `snestorage-logo`, como en el correo actual.

El mensaje no fija una duración concreta: la caducidad debe corresponder a la configuración real del proveedor de tokens. No promete que solicitar otro correo invalide los enlaces previos.

## Publicación

La copia actual del csproj no declara CopyToPublishDirectory para EmailTemplates. Asegurar que el HTML, TXT y logo lleguen al despliegue. Por ejemplo, añadir dentro del proyecto:

```xml
<ItemGroup>
  <None Update="EmailTemplates\*.html;EmailTemplates\*.txt;EmailTemplates\assets\*.png"
        CopyToOutputDirectory="PreserveNewest"
        CopyToPublishDirectory="PreserveNewest" />
</ItemGroup>
```

Este cambio de proyecto no se aplicó. No se envió un email real ni se verificó el renderizado en clientes de correo.
