# Reenvío de confirmación de email

Propuesta para aplicar tú en backend. El frontend está preparado para el contrato siguiente. No requiere migración.

**Estado observado al terminar el frontend:** ya aplicaste los cuatro pasos, incluido el resultado del envío en `Register`. El frontend está conectado a esas acciones. No se hicieron envíos reales.

## Contrato

- `POST /api/account/confirmation/resend`.
- Body JSON: un string, por ejemplo `"player@example.com"`, igual que en la solicitud de cambio de contraseña.
- CAPTCHA action: `email_confirmation_resend`.
- Política HTTP: `password-email` (comparte el cupo de 3 solicitudes cada 15 minutos por IP con la recuperación de contraseña).
- Éxito: `Ok("AccountController-Success")`, mediante tu mapper existente.
- Errores: tus mappers existentes de AccountServiceResult y EmailServiceResult.
- Cuenta inexistente o ya confirmada: la misma respuesta de éxito neutral. El frontend no afirma que exista una cuenta ni que el correo se haya enviado en esos casos.

## 1. AccountService: añadir el método

Los imports necesarios (`OneOf`, `WebEncoders`, `Encoding`) ya están en AccountService.

```csharp
public async Task<OneOf<AccountServiceResult, EmailServiceResult>> RequestEmailConfirmation(
    string email, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(email))
        return AccountServiceResult.InvalidEmail;

    var userRes = await _dbHelperService.TryFunction(
        async _ => await _userManager.FindByEmailAsync(email.Trim()),
        AccountServiceResult.DBError, ct: ct);
    if (!userRes.TryPickT0(out var user, out var error))
        return error;
    if (user == null || user.EmailConfirmed)
        return AccountServiceResult.Success;

    var tokenRes = await _dbHelperService.TryFunction(
        async _ => await _userManager.GenerateEmailConfirmationTokenAsync(user),
        AccountServiceResult.TokenCreationError, ct: ct);
    if (!tokenRes.TryPickT0(out var token, out error))
        return error;

    var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    var emailResult = await _emailService.Confirmation(
        user.UserName!, user.Id, user.Email!, encodedToken, ct);
    if (emailResult != EmailServiceResult.Success)
        return emailResult;

    return AccountServiceResult.Success;
}
```

Solo usa el email guardado en la cuenta y el template existente. No confirma la cuenta automáticamente. GenerateEmailConfirmationTokenAsync conserva la caducidad configurada en Identity; el frontend no recibe el token. Reenviar no garantiza que los enlaces anteriores queden invalidados.

## 2. AccountController: añadir la acción

Añade este import:

```csharp
using System.ComponentModel.DataAnnotations;
```

Añade la acción dentro del controlador. El Route de la clase ya aporta `api/account`.

```csharp
[AllowAnonymous]
[EnableRateLimiting("password-email")]
[RequireCaptcha("email_confirmation_resend")]
[HttpPost("confirmation/resend")]
public async Task<ActionResult> RequestEmailConfirmation(
    [FromBody, Required, EmailAddress] string email, CancellationToken ct)
{
    var res = await _accountService.RequestEmailConfirmation(email, ct);
    return GetActionResult(res);
}
```

Tu overload de GetActionResult(OneOf<AccountServiceResult, EmailServiceResult>) ya existe. No añadas otro. El filtro antiforgery global también protege esta acción; el frontend envía el token mediante el transporte existente.

## 3. RegisterResponseDTO: informar del envío inicial

Añade esta propiedad:

```csharp
public bool ConfirmationEmailSent { get; set; }
```

Conserva UserId y RequiresEmailConfirmation.

## 4. AccountService.Register: sustituir únicamente el final

Después de guardar y confirmar la transacción, sustituye el bloque que genera el token/envía el email y el return final por:

```csharp
bool confirmationEmailSent = false;
var confirmationToken = await _dbHelperService.TryFunction(
    async _ => await _userManager.GenerateEmailConfirmationTokenAsync(user), ct: ct);

if (!string.IsNullOrWhiteSpace(confirmationToken))
{
    var encodedToken = WebEncoders.Base64UrlEncode(
        Encoding.UTF8.GetBytes(confirmationToken));
    var emailResult = await _emailService.Confirmation(
        user.UserName!, user.Id, user.Email!, encodedToken, ct);
    confirmationEmailSent = emailResult == EmailServiceResult.Success;
}

return new RegisterResponseDTO
{
    UserId = user.Id,
    ConfirmationEmailSent = confirmationEmailSent
};
```

Esto conserva la cuenta creada si falla la generación del token o EmailService devuelve un error. El frontend dirige a CheckEmail y muestra el aviso de envío fallido cuando ConfirmationEmailSent es false. No devuelve un fallo de registro por un problema de correo después del commit: eso induciría a registrar la misma cuenta de nuevo.

## Frontend preparado

- Formulario en `/Register/CheckEmail`, con email obligatorio, formato validado antes del CAPTCHA y un botón de reenvío.
- CAPTCHA, antiforgery y mensajes para errores de SMTP, validación y rate limiting.
- La dirección introducida se conserva si falla; no se reintenta automáticamente una petición de envío.
- Acceso desde Login y desde enlaces de confirmación inválidos/incompletos.
- El registro añade `?delivery=failed` a CheckEmail si la respuesta indica fallo de envío. No añade email ni token a esa URL.

## Verificación manual después de aplicar backend

1. Registrarse normalmente: CheckEmail y correo de confirmación.
2. Reenviar para una cuenta sin confirmar: mensaje neutral y email nuevo.
3. Email mal formado: validación del formulario antes del CAPTCHA.
4. Email inexistente o ya confirmado: el mismo mensaje neutral.
5. Superar el cupo: mensaje de espera, sin vaciar el campo.
6. Si falla el envío inicial: cuenta conservada, aviso y formulario de reenvío abierto.

El SMTP puede aceptar un mensaje aunque su entrega final se retrase o falle; ConfirmationEmailSent indica el éxito reportado por tu EmailService, no que el destinatario haya recibido el mensaje.

## Rutas del avatar — corregido

Revisión posterior: Register y EditUserInfo ya pasan ContentType a GetAvatarUrl. Las dos llamadas están corregidas; este bloque se conserva como referencia de la corrección:

```csharp
// Register
var path = GetAvatarUrl(avatar.ContentType, user.Id, user.UserName);

// EditUserInfo
var path = GetAvatarUrl(dto.File.ContentType, userId, user);
```

La validación de archivos en FileService sigue siendo necesaria. El usuario aplicó estas correcciones en backend. Compilación comprobada: 0 errores y 7 advertencias existentes. No se hicieron registros ni envíos reales durante esta revisión.
