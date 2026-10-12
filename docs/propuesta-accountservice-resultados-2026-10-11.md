# Propuesta: AccountService con resultados explícitos

Fecha: 2026-10-11.

Documento basado en la copia actual de AccountService, DBHelperService, AccountController, RegisterController, LoginController, ProfileController y sus consumidores. Es una propuesta para aplicar por partes: no se modificó el backend ni se ejecutaron pruebas de los fragmentos.

## 1. Qué cambiaría

1. Operaciones sin datos: `Task<AccountServiceResult>`.
2. Operaciones con datos: `Task<OneOf<T, AccountServiceResult>>`.
3. Operaciones con archivos: conservar `FileServiceResult` como alternativa para no perder el motivo concreto del fallo.
4. Usar DBHelperService para capturar excepciones; los chequeos de existencia, permisos e IdentityResult siguen en AccountService.
5. Traducir enums a HTTP en AccountController, con cuerpos como `AccountController-UserNotFound`.
6. RegisterController y LoginController conservan sus rutas web, formularios y redirecciones.
7. Adaptar también ProfileController: consume EditUserInfo y dejaría de compilar con su firma nueva.

No convertiría `IsModeratorOrAdmin()` a un enum: su bool representa una pregunta de permisos, no una operación que guarda datos. `GetUserID()` tampoco necesita cambiar a un resultado de operación.

### Contratos propuestos

| Método | Resultado actual | Resultado propuesto |
|---|---|---|
| Register | OneOf con DTO, error de cuenta y error de archivo | Mantener las tres alternativas |
| Login | AuthenticationResponse? | OneOf<AuthenticationResponse, AccountServiceResult> |
| Logout | bool | AccountServiceResult |
| RefreshToken | AuthenticationResponse? | OneOf<AuthenticationResponse, AccountServiceResult> |
| BuildToken | AuthenticationResponse? | OneOf<AuthenticationResponse, AccountServiceResult> |
| AddRole / RemoveRole | bool | AccountServiceResult |
| EditUserInfo | bool | OneOf<AccountServiceResult, FileServiceResult> |
| GetUserInfo | OneOf<UserInfo, AccountServiceResult> | Mantener contrato, usar helper |
| ConfirmUser | AccountServiceResult | Mantener contrato, usar helper |
| GetUser | ApplicationUser? | Mantener adaptador temporal; añadir GetUserResult |

`OneOf<AccountServiceResult, FileServiceResult>` no contiene un DTO: su alternativa de cuenta puede ser Success o un error. El controlador debe comprobar el enum, no asumir que IsT0 significa éxito.

## 2. Enum: conservar los valores existentes

Añadir los nuevos miembros al final evita cambiar los números de los antiguos durante la transición. Aunque las respuestas nuevas serán strings, todavía hay consumidores que interpretan enums numéricos.

Reemplazo propuesto del enum:

```csharp
public enum AccountServiceResult
{
    UserNotFound,
    InvalidToken,
    UserAlreadyExists,
    CancelledToken,
    AvatarLoadError,
    UserCreationError,
    TokenCreationError,
    DBError,
    Success,
    Error,

    Unauthenticated,
    InvalidInput,
    InvalidCredentials,
    EmailNotConfirmed,
    LockedOut,
    RoleAlreadyAssigned,
    RoleNotAssigned,
    RoleUpdateError,
    LogoutError,
    CacheError
}
```

Distinciones útiles:

- UserNotFound: el usuario o su perfil requerido no existe.
- Unauthenticated: falta una sesión autenticada o el identificador del usuario.
- InvalidCredentials: login rechazado por credenciales incorrectas. No devolver UserNotFound en el login público.
- EmailNotConfirmed / LockedOut: razones de rechazo del inicio de sesión.
- UserCreationError / RoleUpdateError: Identity devolvió Succeeded = false; no necesariamente ocurrió una excepción de DB.
- DBError: excepción durante una operación de acceso a datos.
- TokenCreationError: fallo al construir o firmar el JWT.
- CancelledToken: una cancelación vinculada al token recibido.

Error se mantiene por compatibilidad; el código nuevo debe preferir un motivo específico.

## 3. DBHelperService: una sobrecarga pequeña

El helper actual captura todas las excepciones y devuelve un error fijo. Así se pierde la diferencia entre cancelación y error de DB. Además, su sobrecarga nullable no convierte un int en int?: para ello hay que pedir explícitamente `T = int?`.

Propongo añadir esta sobrecarga, conservando las actuales:

```csharp
public async Task<T> TryFunction<T>(
    Func<CancellationToken, Task<T>> func,
    Func<Exception, T> errorFactory,
    CancellationToken ct = default)
{
    try
    {
        return await func(ct);
    }
    catch (Exception exception)
    {
        return errorFactory(exception);
    }
}
```

Aquí T puede ser un enum, DTO o OneOf. No cambia el funcionamiento de las llamadas existentes. En los ejemplos se especifica el tipo genérico para que la sobrecarga seleccionada sea explícita.

No utilizar esta sobrecarga para esconder fallos de limpieza: registrar esos fallos por separado cuando corresponda.

### Dos adaptadores cortos en AccountService

Estos evitan repetir la clasificación en cada método. No contienen queries ni lógica de negocio.

```csharp
private static AccountServiceResult OperationError(
    Exception exception,
    CancellationToken ct,
    AccountServiceResult fallback)
{
    return exception is OperationCanceledException && ct.IsCancellationRequested
        ? AccountServiceResult.CancelledToken
        : fallback;
}

private Task<AccountServiceResult> RunAccount(
    Func<CancellationToken, Task<AccountServiceResult>> operation,
    CancellationToken ct,
    AccountServiceResult fallback = AccountServiceResult.DBError)
{
    return _dbHelperService.TryFunction<AccountServiceResult>(
        operation,
        exception => OperationError(exception, ct, fallback),
        ct);
}

private Task<OneOf<T, AccountServiceResult>> QueryAccount<T>(
    Func<CancellationToken, Task<T>> operation,
    CancellationToken ct,
    AccountServiceResult fallback = AccountServiceResult.DBError)
{
    return _dbHelperService.TryFunction<OneOf<T, AccountServiceResult>>(
        async cancel => await operation(cancel),
        exception => OperationError(exception, ct, fallback),
        ct);
}
```

Los null de negocio siguen siendo null dentro de la alternativa T. Por ejemplo, QueryAccount<ApplicationUser?> devuelve una alternativa de datos con null cuando la búsqueda no encuentra al usuario; eso debe traducirse a UserNotFound o InvalidCredentials según el contexto.

## 4. Consultas de perfil

### GetUserID

Evitar el operador `!` sobre HttpContext: en contextos sin petición puede ser null.

```csharp
public string? GetUserID()
{
    return _httpContextAccessor.HttpContext?.User
        .FindFirstValue(ClaimTypes.NameIdentifier);
}
```

### GetUserInfo

Se conservan las relaciones necesarias para UserNameDTO y la vista de perfil.

```csharp
public Task<OneOf<UserInfo, AccountServiceResult>> GetUserInfo(
    CancellationToken ct)
{
    var context = _httpContextAccessor.HttpContext;
    var userId = GetUserID();

    if (context?.User.Identity?.IsAuthenticated != true ||
        string.IsNullOrWhiteSpace(userId))
    {
        return Task.FromResult<OneOf<UserInfo, AccountServiceResult>>(
            AccountServiceResult.Unauthenticated);
    }

    return GetUserInfo(userId, ct);
}

public async Task<OneOf<UserInfo, AccountServiceResult>> GetUserInfo(
    string userId, CancellationToken ct)
{
    var result = await QueryAccount<UserInfo?>(cancel => _context.UserInfos
        .AsNoTracking()
        .Include(info => info.TimeZone)
        .Include(info => info.EmailVisibility)
        .Include(info => info.Avatar)
        .Include(info => info.User)
        .FirstOrDefaultAsync(info => info.UserId == userId, cancel), ct);

    if (!result.TryPickT0(out var info, out var error))
        return error;

    if (info == null)
        return AccountServiceResult.UserNotFound;

    info.UserSummary = UserNameDTO.FromUserInfo(info, this);
    return info;
}
```

### GetUserResult y compatibilidad con GetUser

UserNameDTO.FromUser llama actualmente GetUser(). No cambiar su firma sin modificar también ese consumidor.

```csharp
public async Task<OneOf<ApplicationUser, AccountServiceResult>> GetUserResult(
    CancellationToken ct = default)
{
    var userId = GetUserID();
    if (string.IsNullOrWhiteSpace(userId))
        return AccountServiceResult.Unauthenticated;

    var result = await QueryAccount<ApplicationUser?>(
        async cancel => await _context.Users.FindAsync([userId], cancel), ct);

    if (!result.TryPickT0(out var user, out var error))
        return error;

    return user == null
        ? AccountServiceResult.UserNotFound
        : user;
}

// Adaptador temporal para los consumidores actuales.
public async Task<ApplicationUser?> GetUser(CancellationToken ct = default)
{
    var result = await GetUserResult(ct);
    return result.TryPickT0(out var user, out _) ? user : null;
}
```

El adaptador pierde el motivo del error intencionalmente para conservar el contrato antiguo. Los nuevos consumidores deben usar GetUserResult. Tampoco añade automáticamente UserInfo/Avatar: si un consumidor los necesita, debe usar GetUserInfo o una proyección adecuada.

## 5. Login, JWT y refresh

La propuesta usa CheckPasswordSignInAsync para validar el login sin crear una cookie de Identity antes de saber si se pudo emitir el JWT. Tu flujo web guarda posteriormente ese JWT en Session desde LoginController.

Este cambio es deliberado: si tienes otro consumidor que depende de la cookie creada por PasswordSignInAsync, deberá conservar su flujo de cookie aparte. No es necesario para el flujo Session + JWT descrito aquí.

Las APIs de UserManager/SignInManager usadas aquí no reciben CancellationToken. Añadir ct a AccountService no significa que esas llamadas se vuelvan cancelables; ct sí se utiliza para clasificar las excepciones y las operaciones de EF que lo admiten.

### Login

```csharp
public async Task<OneOf<AuthenticationResponse, AccountServiceResult>> Login(
    LoginCredentials credentials, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(credentials.User) ||
        string.IsNullOrEmpty(credentials.Password))
        return AccountServiceResult.InvalidInput;

    var userResult = await QueryAccount<ApplicationUser?>(
        _ => _userManager.FindByNameAsync(credentials.User), ct);

    if (!userResult.TryPickT0(out var user, out var error))
        return error;

    if (user == null)
        return AccountServiceResult.InvalidCredentials;

    var signInResult = await QueryAccount<SignInResult>(
        _ => _signInManager.CheckPasswordSignInAsync(
            user, credentials.Password, lockoutOnFailure: true), ct);

    if (!signInResult.TryPickT0(out var signIn, out error))
        return error;

    if (signIn.IsLockedOut)
        return AccountServiceResult.LockedOut;

    if (!signIn.Succeeded)
    {
        // RequireConfirmedEmail sigue configurado en Identity.
        // IsNotAllowed puede tener otras causas si añades más políticas.
        return signIn.IsNotAllowed && !user.EmailConfirmed
            ? AccountServiceResult.EmailNotConfirmed
            : AccountServiceResult.InvalidCredentials;
    }

    return await BuildToken(user, ct);
}
```

Este ejemplo mantiene la distinción EmailNotConfirmed que quieres mostrar. Si a futuro necesitas ocultar también ese estado para evitar enumeración de cuentas, unifica los rechazos públicos de login en InvalidCredentials.

### BuildToken: reutilizar el usuario ya obtenido

La sobrecarga privada evita volver a buscar el usuario después del login.

```csharp
public async Task<OneOf<AuthenticationResponse, AccountServiceResult>> BuildToken(
    string username, CancellationToken ct = default)
{
    var result = await QueryAccount<ApplicationUser?>(
        _ => _userManager.FindByNameAsync(username), ct);

    if (!result.TryPickT0(out var user, out var error))
        return error;

    if (user == null)
        return AccountServiceResult.UserNotFound;

    return await BuildToken(user, ct);
}

private async Task<OneOf<AuthenticationResponse, AccountServiceResult>> BuildToken(
    ApplicationUser user, CancellationToken ct)
{
    if (!user.EmailConfirmed)
        return AccountServiceResult.EmailNotConfirmed;

    var lockoutResult = await QueryAccount<bool>(
        _ => _userManager.IsLockedOutAsync(user), ct);

    if (!lockoutResult.TryPickT0(out var lockedOut, out var error))
        return error;

    if (lockedOut)
        return AccountServiceResult.LockedOut;

    var claimsResult = await QueryAccount<IList<Claim>>(
        _ => _userManager.GetClaimsAsync(user), ct);

    if (!claimsResult.TryPickT0(out var storedClaims, out error))
        return error;

    return await QueryAccount<AuthenticationResponse>(_ =>
    {
        var claims = new List<Claim>
        {
            new("user", user.UserName!),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName!)
        };
        claims.AddRange(storedClaims);

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["JWTKey"]!));
        var credentials = new SigningCredentials(
            key, SecurityAlgorithms.HmacSha256);
        var expiration = DateTime.UtcNow.AddMinutes(30);
        var jwt = new JwtSecurityToken(
            claims: claims, expires: expiration,
            signingCredentials: credentials);

        return Task.FromResult(new AuthenticationResponse
        {
            Token = new JwtSecurityTokenHandler().WriteToken(jwt),
            ExpireTime = expiration
        });
    }, ct, AccountServiceResult.TokenCreationError);
}
```

### RefreshToken

Buscar por NameIdentifier evita depender del claim personalizado `user`.

```csharp
public async Task<OneOf<AuthenticationResponse, AccountServiceResult>> RefreshToken(
    CancellationToken ct = default)
{
    var result = await GetUserResult(ct);
    if (!result.TryPickT0(out var user, out var error))
        return error;

    return await BuildToken(user, ct);
}
```

Esto conserva tu refresh actual: exige un JWT válido mediante Authorize. No introduce un sistema nuevo de refresh tokens para renovar JWT expirados.

### Logout

```csharp
public Task<AccountServiceResult> Logout(CancellationToken ct = default)
{
    var context = _httpContextAccessor.HttpContext;
    if (context == null)
        return Task.FromResult(AccountServiceResult.Unauthenticated);

    return RunAccount(async _ =>
    {
        await _signInManager.SignOutAsync();
        context.Session.Remove("Token");
        return AccountServiceResult.Success;
    }, ct, AccountServiceResult.LogoutError);
}
```

Success conserva el significado actual: se limpió la sesión/cookie de este navegador. No revoca copias de JWT que otro cliente ya tenga.

## 6. Roles

Los ejemplos conservan tus roles implementados como claims con valor "1". No los convierten a IdentityRole ni duplican llamadas al servicio.

```csharp
public Task<AccountServiceResult> AddRole(
    EditRole dto, CancellationToken ct = default)
{
    return RunAccount(async _ =>
    {
        var user = await _userManager.FindByNameAsync(dto.Username);
        if (user == null)
            return AccountServiceResult.UserNotFound;

        var claims = await _userManager.GetClaimsAsync(user);
        if (claims.Any(c => c.Type == dto.RoleName && c.Value == "1"))
            return AccountServiceResult.RoleAlreadyAssigned;

        var result = await _userManager.AddClaimAsync(
            user, new Claim(dto.RoleName, "1"));

        return result.Succeeded
            ? AccountServiceResult.Success
            : AccountServiceResult.RoleUpdateError;
    }, ct);
}

public Task<AccountServiceResult> RemoveRole(
    EditRole dto, CancellationToken ct = default)
{
    return RunAccount(async _ =>
    {
        var user = await _userManager.FindByNameAsync(dto.Username);
        if (user == null)
            return AccountServiceResult.UserNotFound;

        var claims = await _userManager.GetClaimsAsync(user);
        var claim = claims.FirstOrDefault(c =>
            c.Type == dto.RoleName && c.Value == "1");

        if (claim == null)
            return AccountServiceResult.RoleNotAssigned;

        var result = await _userManager.RemoveClaimAsync(user, claim);
        return result.Succeeded
            ? AccountServiceResult.Success
            : AccountServiceResult.RoleUpdateError;
    }, ct);
}
```

Si decides limitar RoleName a Admin/Moderator, añade un chequeo explícito al inicio y devuelve InvalidInput. No se incluyó esa restricción para no asumir que nunca añadirás otros claims de rol.

## 7. Confirmación de email

Decode inválido es InvalidToken; una excepción de la consulta o de Identity es DBError. No interpretar todos los resultados desconocidos como éxito.

```csharp
public async Task<AccountServiceResult> ConfirmUser(
    string userId, string token64, CancellationToken ct = default)
{
    if (string.IsNullOrWhiteSpace(userId) ||
        string.IsNullOrWhiteSpace(token64))
        return AccountServiceResult.InvalidToken;

    string? token = _dbHelperService.TryFunction(() =>
        Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token64)));

    if (token == null)
        return AccountServiceResult.InvalidToken;

    return await RunAccount(async _ =>
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return AccountServiceResult.UserNotFound;

        var result = await _userManager.ConfirmEmailAsync(user, token);
        return result.Succeeded
            ? AccountServiceResult.Success
            : AccountServiceResult.InvalidToken;
    }, ct);
}
```

Se conserva el token Base64Url de tu correo. Identity sigue verificando firma, usuario y vigencia; la decodificación por sí sola no valida el token.

## 8. EditUserInfo: datos + archivo + guardado + limpieza

Añadir `ILogger<AccountService>` al constructor y guardar `_logger` si aplicas los avisos de limpieza del ejemplo.

Se conservan los significados del DTO: null = sin cambios; string vacío en redes = quitar enlace. No cambia la fecha de nacimiento, username ni email.

```csharp
public async Task<OneOf<AccountServiceResult, FileServiceResult>> EditUserInfo(
    EditProfileDTO dto, CancellationToken ct)
{
    var principal = _httpContextAccessor.HttpContext?.User;
    var userId = GetUserID();
    var username = principal?.FindFirstValue(ClaimTypes.Name);

    if (principal?.Identity?.IsAuthenticated != true ||
        string.IsNullOrWhiteSpace(userId) ||
        string.IsNullOrWhiteSpace(username))
        return AccountServiceResult.Unauthenticated;

    var infoResult = await QueryAccount<UserInfo?>(cancel => _context.UserInfos
        .Include(info => info.Avatar)
        .FirstOrDefaultAsync(info => info.UserId == userId, cancel), ct);

    if (!infoResult.TryPickT0(out var info, out var error))
        return error;
    if (info == null)
        return AccountServiceResult.UserNotFound;

    var previousAvatar = info.Avatar;
    Models.File? newAvatar = null;
    string? batchPath = null;

    if (dto.File != null)
    {
        if (string.IsNullOrWhiteSpace(dto.File.FileName))
            return AccountServiceResult.InvalidInput;

        batchPath = await _fileService.BeginBatch(ct);
        if (batchPath == null)
            return ct.IsCancellationRequested
                ? AccountServiceResult.CancelledToken
                : AccountServiceResult.AvatarLoadError;

        // El límite específico de avatar/contexto sigue siendo una
        // responsabilidad de la validación de archivos, no del enum.
        var prepared = await _fileService.PrepareFile(
            dto.File, GetAvatarUrl(dto.File.FileName, userId, username),
            batchPath, ct);

        if (!prepared.TryPickT0(out newAvatar, out var fileError))
            return fileError;

        info.Avatar = newAvatar;
    }

    info.UsernameColor = dto.UsernameColor ?? info.UsernameColor;
    info.Bio = dto.Bio ?? info.Bio;
    info.Patreon = GetSocialNetwork(dto.Patreon, info.Patreon);
    info.Paypal = GetSocialNetwork(dto.Paypal, info.Paypal);
    info.BuyMeACoffee = GetSocialNetwork(dto.BuyMeACoffee, info.BuyMeACoffee);
    info.Discord = GetSocialNetwork(dto.Discord, info.Discord);
    info.Twitter = GetSocialNetwork(dto.Twitter, info.Twitter);
    info.Facebook = GetSocialNetwork(dto.Facebook, info.Facebook);
    info.Youtube = GetSocialNetwork(dto.Youtube, info.Youtube);
    info.Instagram = GetSocialNetwork(dto.Instagram, info.Instagram);
    info.Steam = GetSocialNetwork(dto.Steam, info.Steam);
    info.Twitch = GetSocialNetwork(dto.Twitch, info.Twitch);
    info.Github = GetSocialNetwork(dto.Github, info.Github);
    info.NintendoNetwork = GetSocialNetwork(dto.NintendoNetwork, info.NintendoNetwork);
    info.TimeZoneId = dto.TimeZoneId ?? info.TimeZoneId;
    info.EmailVisibilityId = dto.EmailVisibilityId ?? info.EmailVisibilityId;

    var saved = await QueryAccount<int>(
        cancel => _context.SaveChangesAsync(cancel), ct);

    if (saved.TryPickT1(out error, out _))
    {
        if (batchPath != null)
            await _fileService.BatchRollback(batchPath, CancellationToken.None);
        else
            _context.ChangeTracker.Clear();

        return error;
    }

    if (batchPath != null)
        _fileService.BatchComplete(batchPath);

    if (newAvatar != null && previousAvatar != null)
        await CleanupPreviousAvatar(previousAvatar, ct);

    return AccountServiceResult.Success;
}

private async Task CleanupPreviousAvatar(Models.File previousAvatar, CancellationToken ct)
{
    bool cleaned = await _dbHelperService.TryFunction(async cancel =>
    {
        await _context.Files
            .Where(file => file.Id == previousAvatar.Id)
            .ExecuteDeleteAsync(cancel);

        System.IO.File.Delete(Path.Combine(
            _environment.ContentRootPath, previousAvatar.URL));
    }, ct: ct);

    if (!cleaned)
        _logger.LogWarning("Previous avatar cleanup failed for file {FileId}.",
            previousAvatar.Id);
}
```

El rollback recibe un token independiente porque el token de la petición puede estar cancelado precisamente cuando necesitas limpiar. La limpieza del avatar viejo puede quedar pendiente si se cancela ct; no debe convertir un perfil ya guardado en una respuesta de fracaso. Este ejemplo registra el fallo, no implementa un proceso de mantenimiento nuevo.

La entidad anterior continúa en el ChangeTracker después de ExecuteDeleteAsync: no debe volver a editarse/guardarse como si existiera. En este flujo no se vuelve a utilizar.

La llamada PrepareFile mantiene su contrato actual. Si quieres que ninguna excepción de lectura/inspección salga de FileService, termina ese manejo allí y devuelve su FileServiceResult concreto; esta refactorización no sustituye las validaciones del archivo.

## 9. Register: conservar la transacción y separar el correo

UserManager.CreateAsync puede guardar en DB antes del SaveChanges explícito de AccountService. Por eso no sustituiría la transacción completa por un único TrySyncContextWithFiles: el registro involucra varios guardados y el commit debe ocurrir antes de BatchComplete.

La propuesta divide el método en preparación, persistencia y correo. Así no necesitas un try enorme en Register.

Usings adicionales para el bloque de persistencia:

```csharp
using Microsoft.EntityFrameworkCore.Storage;
```

### Register

```csharp
public async Task<OneOf<RegisterResponseDTO, AccountServiceResult, FileServiceResult>> Register(
    RegisterInfo dto, CancellationToken ct = default)
{
    var user = new ApplicationUser { UserName = dto.User, Email = dto.Email };
    var batchPath = await _fileService.BeginBatch(ct);
    if (batchPath == null)
        return ct.IsCancellationRequested
            ? AccountServiceResult.CancelledToken
            : AccountServiceResult.AvatarLoadError;

    var prepared = await _fileService.PrepareFile(
        dto.Avatar, GetAvatarUrl(dto.Avatar.FileName, user.Id, dto.User),
        batchPath, ct);

    if (!prepared.TryPickT0(out var avatar, out var fileError))
        return fileError;

    var saved = await PersistRegistration(user, dto, avatar, batchPath, ct);
    if (saved != AccountServiceResult.Success)
        return saved;

    await SendConfirmationAfterRegistration(user, ct);

    return new RegisterResponseDTO
    {
        UserId = user.Id,
        RequiresEmailConfirmation = true
    };
}
```

### PersistRegistration

El ámbito protegido incluye BeginTransaction, CreateAsync, SaveChanges, Commit y Dispose. Su tamaño corresponde a una sola operación atómica; la lógica del email queda fuera.

```csharp
private async Task<AccountServiceResult> PersistRegistration(
    ApplicationUser user,
    RegisterInfo dto,
    Models.File avatar,
    string batchPath,
    CancellationToken ct)
{
    bool committed = false;

    var result = await RunAccount(async cancel =>
    {
        await using var transaction =
            await _context.Database.BeginTransactionAsync(cancel);

        var created = await _userManager.CreateAsync(user, dto.Password);
        if (!created.Succeeded)
        {
            // Dispose revierte esta transacción sin commit.
            return created.Errors.Any(error =>
                error.Code is "DuplicateUserName" or "DuplicateEmail")
                ? AccountServiceResult.UserAlreadyExists
                : AccountServiceResult.UserCreationError;
        }

        _context.UserInfos.Add(new UserInfo
        {
            UserId = user.Id,
            UsernameColor = dto.UsernameColor,
            Birthday = dto.Birthday,
            TimeZoneId = dto.TimeZoneId,
            EmailVisibilityId = dto.EmailVisibilityId,
            Avatar = avatar
        });

        await _context.SaveChangesAsync(cancel);
        await transaction.CommitAsync(cancel);
        committed = true;
        return AccountServiceResult.Success;
    }, ct);

    if (!committed)
    {
        await _fileService.BatchRollback(batchPath, CancellationToken.None);
        return result;
    }

    // Si el commit terminó, no borrar sus archivos por un fallo posterior.
    _fileService.BatchComplete(batchPath);

    if (result != AccountServiceResult.Success)
        _logger.LogWarning("Registration committed but transaction cleanup failed for user {UserId}.", user.Id);

    return AccountServiceResult.Success;
}
```

Esto presupone, como tu implementación actual, que UserManager y AccountService usan el mismo SnestorageContext scoped. Si fueran contextos diferentes, esta transacción no abarcaría ambos automáticamente. Ver [transacciones de EF Core](https://learn.microsoft.com/en-us/ef/core/saving/transactions).

Un fallo de conexión durante Commit puede dejar incierto si el servidor llegó a confirmar: este ejemplo no introduce reconciliación ni reintentos del registro. Conserva la política de rollback del proyecto y no debe venderse como solución a ese escenario extremo.

### SendConfirmationAfterRegistration

No devolver UserCreationError si la cuenta ya existe en DB pero el correo falló. Tu pantalla CheckEmail y el futuro reenvío son el lugar para ese caso.

```csharp
private async Task SendConfirmationAfterRegistration(
    ApplicationUser user, CancellationToken ct)
{
    var sent = await _dbHelperService.TryFunction<EmailServiceResult>(async cancel =>
    {
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        return await _emailService.Confirmation(
            user.UserName!, user.Id, user.Email!, encoded, cancel);
    }, _ => EmailServiceResult.EmailConnectionError, ct);

    if (sent != EmailServiceResult.Success)
        _logger.LogWarning("Confirmation email failed for user {UserId}: {Result}.",
            user.Id, sent);
}
```

Aquí se conservan los EmailServiceResult que devuelve EmailService. Una excepción no clasificada del bloque se registra como EmailConnectionError; si necesitas diferenciar generación de token y SMTP, divide esos dos pasos posteriormente. No registrar el token ni la contraseña.

## 10. AccountController: un mapper y extracción de alternativas

Reemplazo propuesto completo del controlador. Conserva los endpoints actuales y los atributos de autorización/captcha. La acción de registro declara FromForm porque contiene IFormFile.

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNEStorage.DTOs;
using SNEStorage.Filters.Attributes;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController(
    AccountService accountService,
    FileController fileController) : ControllerBase
{
    public AccountService AccountService { get; } = accountService;
    private readonly FileController _fileController = fileController;

    [NonAction]
    public ActionResult GetActionResult(AccountServiceResult result)
    {
        string body = $"AccountController-{result}";

        return result switch
        {
            AccountServiceResult.Success => Ok(body),
            AccountServiceResult.UserNotFound => NotFound(body),
            AccountServiceResult.Unauthenticated or
            AccountServiceResult.InvalidCredentials => Unauthorized(body),
            AccountServiceResult.EmailNotConfirmed or
            AccountServiceResult.LockedOut =>
                StatusCode(StatusCodes.Status403Forbidden, body),
            AccountServiceResult.UserAlreadyExists or
            AccountServiceResult.RoleAlreadyAssigned or
            AccountServiceResult.RoleNotAssigned => Conflict(body),
            AccountServiceResult.InvalidToken or
            AccountServiceResult.InvalidInput or
            AccountServiceResult.UserCreationError => BadRequest(body),
            AccountServiceResult.CacheError =>
                StatusCode(StatusCodes.Status503ServiceUnavailable, body),
            AccountServiceResult.CancelledToken =>
                StatusCode(StatusCodes.Status499ClientClosedRequest, body),
            _ => StatusCode(StatusCodes.Status500InternalServerError, body)
        };
    }

    [NonAction]
    public ActionResult GetFileActionResult(FileServiceResult result, CancellationToken ct)
    {
        // El controlador inyectado no tiene garantizado el ControllerContext
        // de la petición del AccountController. Usar el ct del endpoint.
        if (result == FileServiceResult.CancellationTokenError && ct.IsCancellationRequested)
            return StatusCode(StatusCodes.Status499ClientClosedRequest,
                $"FileController-{result}");

        return _fileController.GetActionResult(result);
    }

    [HttpPost("register")]
    [Consumes("multipart/form-data")]
    [RequireCaptcha("user_register")]
    public async Task<ActionResult<RegisterResponseDTO>> Register(
        [FromForm] RegisterInfo dto, CancellationToken ct)
    {
        var result = await AccountService.Register(dto, ct);
        if (result.TryPickT0(out var response, out var errors))
            return response;

        return errors.TryPickT0(out var accountError, out var fileError)
            ? GetActionResult(accountError)
            : GetFileActionResult(fileError, ct);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthenticationResponse>> Login(
        LoginCredentials dto, CancellationToken ct)
    {
        var result = await AccountService.Login(dto, ct);
        if (result.TryPickT0(out var response, out var error))
            return response;

        return GetActionResult(error);
    }

    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult> Logout(CancellationToken ct)
    {
        return GetActionResult(await AccountService.Logout(ct));
    }

    [HttpGet("refresh-token")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("user_refreshToken")]
    public async Task<ActionResult<AuthenticationResponse>> RefreshToken(CancellationToken ct)
    {
        var result = await AccountService.RefreshToken(ct);
        if (result.TryPickT0(out var response, out var error))
            return response;

        return GetActionResult(error);
    }

    [HttpPost("add-role")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "Admin")]
    public async Task<ActionResult> AddRole(EditRole dto, CancellationToken ct)
    {
        return GetActionResult(await AccountService.AddRole(dto, ct));
    }

    [HttpPost("remove-role")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "Admin")]
    public async Task<ActionResult> RemoveRole(EditRole dto, CancellationToken ct)
    {
        return GetActionResult(await AccountService.RemoveRole(dto, ct));
    }

    internal async Task<IActionResult> ConfirmUser(
        string userId, string token, CancellationToken ct)
    {
        return GetActionResult(await AccountService.ConfirmUser(userId, token, ct));
    }
}
```

No serializar OneOf como body de error: extraer su alternativa activa. FileController conserva sus propios códigos HTTP de archivos y el prefijo FileController.

En acciones con datos se usa `return response`, en lugar de `return Ok(response)`, para conservar response.Value al llamar al AccountController directamente desde los controladores web. Con Ok(response), el contenido está en response.Result como OkObjectResult; Value puede ser null incluso cuando la respuesta es exitosa. Ver [ActionResult<T>](https://learn.microsoft.com/en-us/aspnet/core/web-api/action-return-types?view=aspnetcore-10.0#actionresultt-type).

Los atributos de un controlador llamado directamente como objeto no vuelven a ejecutar filtros MVC. Por eso se conserva RequireCaptcha en RegisterController y la validación de antiforgery en los endpoints web. No suponer que llamar AccountController.Register aplica por segunda vez su captcha.

## 11. RegisterController: errores HTTP y éxito con redirección

Conservar el constructor y GetRegistrationOptions. Reemplazar estas dos acciones:

```csharp
[HttpPost("submit")]
[ValidateAntiForgeryToken]
[RequireCaptcha("user_register")]
public async Task<IActionResult> Register(
    [FromForm] RegisterInfo credentials, CancellationToken ct)
{
    var response = await _accountController.Register(credentials, ct);

    if (response.Value != null)
        return Redirect("/Register/CheckEmail");

    // register.js ya recibe status + body y mantiene los valores del form.
    return response.Result ?? _accountController.GetActionResult(
        AccountServiceResult.Error);
}

[AllowAnonymous]
[HttpPost("confirmed/{userId}")]
public Task<IActionResult> Confirmed(
    [FromRoute] string userId,
    [FromQuery] string token,
    CancellationToken ct)
{
    return _accountController.ConfirmUser(userId, token, ct);
}
```

GetRegistrationOptions: sustituir ambos `StatusCode(503)` sin body por:

```csharp
return _accountController.GetActionResult(AccountServiceResult.CacheError);
```

Aquí el error pertenece al contrato de cuenta aunque la ruta sea api/register/options. El prefijo AccountController es estable porque esa clase centraliza el mapper.

Si RegisterController debe soportar además un POST HTML sin JavaScript, puedes redirigir esos errores al formulario para ese cliente y devolver los errores HTTP para `Accept: application/json`. El formulario actual utiliza register.js; esta propuesta conserva los detalles de error para ese flujo.

## 12. LoginController: conservar returnUrl y revisar el logout

Mantiene el POST de formulario. Los fallos de login seguirán redirigiendo con `result=invalid`, que el frontend actual entiende. El endpoint api/account/login conserva el motivo específico.

```csharp
[HttpPost("submit")]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Login(
    [FromForm] LoginCredentials credentials,
    [FromForm] string? returnUrl = null,
    CancellationToken ct = default)
{
    var destination = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
    var response = await _accountController.Login(credentials, ct);

    if (response.Value == null)
    {
        // No disfrazar un fallo de servidor como contraseña incorrecta.
        if (response.Result is ObjectResult failure && failure.StatusCode >= 500)
            return failure;

        if (response.Result is ObjectResult cancelled && cancelled.StatusCode == 499)
            return cancelled;

        return Redirect("/Login?result=invalid&returnUrl=" +
            Uri.EscapeDataString(destination));
    }

    HttpContext.Session.SetString("Token", response.Value.Token);
    return LocalRedirect(destination);
}

[HttpPost("logout")]
[ValidateAntiForgeryToken]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public async Task<IActionResult> Logout(CancellationToken ct)
{
    var response = await _accountController.Logout(ct);

    return response is OkObjectResult { StatusCode: StatusCodes.Status200OK }
        ? Redirect("/")
        : Redirect("/?result=logout_failed");
}
```

Importante: el código actual comprueba `is OkResult`. El nuevo mapper retorna `Ok(body)`, que es OkObjectResult. Sin este ajuste, el logout podría completarse y aun así redirigir como si hubiera fallado.

Mejora posterior del frontend: distinguir email pendiente, bloqueo temporal y fallo de servidor en la pantalla Login mediante mensajes apropiados. No pasar contraseñas, JWT ni detalles internos por query string. Este documento conserva `result=invalid` para los rechazos de login y no requiere esa mejora para compilar.

## 13. ProfileController: cambio obligatorio aunque no estuviera en la lista inicial

Si EditUserInfo cambia de bool a OneOf, este controlador también necesita ajuste. Añadir AccountController al constructor y guardar `_accountController`.

```csharp
[HttpGet("me")]
[ProducesResponseType<UserInfo>(StatusCodes.Status200OK)]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public async Task<ActionResult<UserInfo?>> GetMyProfile(CancellationToken ct)
{
    var result = await _accountService.GetUserInfo(ct);
    if (result.TryPickT0(out var info, out var error))
        return Ok(info);

    return _accountController.GetActionResult(error);
}

[HttpPut("me/edit")]
[Consumes("multipart/form-data")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
public async Task<ActionResult> EditMyProfile(
    [FromForm] EditProfileDTO dto, CancellationToken ct)
{
    var result = await _accountService.EditUserInfo(dto, ct);

    return result.TryPickT0(out var accountResult, out var fileError)
        ? _accountController.GetActionResult(accountResult)
        : _accountController.GetFileActionResult(fileError, ct);
}
```

No cambiar las rutas `api/profile/me` ni `api/profile/me/edit`. La navegación de cuenta y el editor dependen de ellas.

## 14. Cambios del frontend cuando apliques la propuesta

No se aplicaron ahora. Corresponden a la siguiente integración, después de que definas el contrato definitivo.

| Archivo / flujo | Ajuste |
|---|---|
| Components/ApiErrorPresentation.cs | Añadir mensajes en inglés para los enums nuevos; mantener lectura de prefijos AccountController/FileController |
| Perfil / editor de perfil | Conservar lectura de errores por status + body; éxito sigue siendo HTTP 200 |
| Register.razor.cs y register.js | El éxito sigue redirigiendo a CheckEmail; los errores HTTP conservarán el formulario y mostrarán el motivo |
| Login.razor.cs | Opcional: mensajes específicos para email sin confirmar y bloqueo cuando se cambie la redirección web |
| Menú de cuenta | Logout web sigue redirigiendo; no cambiar su endpoint |

Los rechazos automáticos de Authorize, antiforgery, validación de ModelState o captcha pueden ocurrir antes de entrar al action y no necesariamente llevan el prefijo AccountController. El frontend debe conservar sus fallbacks actuales para esos formatos.

## 15. Orden para aplicarlo

1. Añadir enums al final y la sobrecarga de DBHelperService.
2. Añadir RunAccount / QueryAccount y convertir roles, ConfirmUser, Logout y GetUserInfo.
3. Convertir Login, BuildToken y RefreshToken juntos para evitar consumidores con firmas incompatibles.
4. Añadir GetUserResult y mantener GetUser como adaptador temporal.
5. Convertir EditUserInfo y adaptar ProfileController en el mismo cambio.
6. Refactorizar la persistencia y el correo del registro, conservando su transacción.
7. Aplicar AccountController y las acciones web nuevas de RegisterController/LoginController conjuntamente.
8. Adaptar mensajes del frontend a los enums definitivos.

### Comprobaciones sugeridas para cuando lo implementes

- Registro correcto: usuario, UserInfo y avatar guardados; sin JWT antes de confirmar email.
- Fallo de CreateAsync/SaveChanges: transacción revertida y batch limpiado.
- Fallo de email después del commit: cuenta conservada y pantalla CheckEmail.
- Login inválido, email pendiente y cuenta bloqueada: no JWT ni sesión nueva.
- Login correcto: Session.Token y vuelta a un returnUrl local válido.
- Logout: HTTP de éxito detectado aunque el mapper use OkObjectResult.
- Edición sin avatar: no borrar el avatar actual.
- Edición con avatar: guardar relación nueva antes de limpiar el anterior.
- Fallo de limpieza del avatar: Success del perfil más log de mantenimiento.
- GetMyProfile: distinguir 401, 404, 499 y 500, sin convertir todos en BadRequest.
- Error de archivos: body FileController-NombreDelError, sin serializar OneOf.

Estos son casos sugeridos, no pruebas ejecutadas ni garantías de compilación de los fragmentos. La propuesta requiere los cambios de firmas, enum, helper e inyección descritos arriba para aplicarse como conjunto.
