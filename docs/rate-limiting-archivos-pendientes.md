# Punto 3: archivos con los ajustes pendientes

Basado en la copia actual del proyecto, revisada el 2026-10-11. Las versiones de código de este documento son propuestas para que las copies tú: los archivos de backend originales no se editaron.

Solo se añaden los atributos de rate limiting y las validaciones de longitud que faltan. Los cuerpos de los métodos, rutas, contratos, CAPTCHA y permisos se conservan tal como están en tu código actual. Si haces cambios adicionales después de esta revisión, compara antes de reemplazar un archivo completo.

## Program.cs: únicamente el import pendiente

Tu registro de políticas, el límite de dos subidas simultáneas, OnRejected, UseRateLimiter y la restauración de IgnoreCycles ya están aplicados. No se reproducen esos bloques como tareas pendientes.

Añade junto a los demás imports:

```csharp
using Microsoft.AspNetCore.RateLimiting;
```

Lo necesita EnableRateLimitingAttribute, que ya usas al identificar las acciones de uploads en GlobalLimiter.

## Archivos completos propuestos

En cada controlador se añade ese mismo import y un atributo EnableRateLimiting a las acciones indicadas. ResourceController y CommentController incluyen además StringLength(5000) en los parámetros commentText. EditProfileDTO incluye StringLength(2000) en Bio.

- [LoginController.cs](#logincontrollercs)
- [AccountController.cs](#accountcontrollercs)
- [RegisterController.cs](#registercontrollercs)
- [ResourceController.cs](#resourcecontrollercs)
- [ProfileController.cs](#profilecontrollercs)
- [CommentController.cs](#commentcontrollercs)
- [NewsController.cs](#newscontrollercs)
- [UserController.cs](#usercontrollercs)
- [VideogameController.cs](#videogamecontrollercs)
- [ExternalAuthorController.cs](#externalauthorcontrollercs)
- [EditProfileDTO.cs](#editprofiledtocs)

## LoginController.cs

Archivo destino: `SNEStorage/Controllers/LoginController.cs`.

Cambios pendientes incluidos:

- `Login`: `login`.

```csharp
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNEStorage.DTOs;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/login")]
public class LoginController(AccountController accountController) : Controller
{
    private readonly AccountController _accountController = accountController;

    [EnableRateLimiting("login")]
    [HttpPost("submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromForm] LoginCredentials credentials, [FromForm] string? returnUrl = null)
    {
        var destination = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
        var response = await _accountController.Login(credentials);
        if (response.Value is null)
        {
            return Redirect("/Login?result=invalid&returnUrl=" +
                Uri.EscapeDataString(destination));
        }

        HttpContext.Session.SetString("Token", response.Value.Token);
        return LocalRedirect(destination);
    }
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Logout()
    {
        return await _accountController.Logout() is OkResult
            ? Redirect("/")
            : Redirect("/?result=logout_failed");
    }
}
```

## AccountController.cs

Archivo destino: `SNEStorage/Controllers/AccountController.cs`.

Cambios pendientes incluidos:

- `ChangePassword`: `writes`.
- `RequestChangePassword`: `password-email`.
- `Register`: `register`.
- `Login`: `login`.
- `AddRole`: `writes`.
- `RemoveRole`: `writes`.

```csharp
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Filters.Attributes;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController(AccountService accountService,
    EmailController emailController,
    FileController fileController) : ControllerBase
{
    private readonly AccountService _accountService = accountService;
    private readonly EmailController _emailController = emailController;
    private readonly FileController _fileController = fileController;
    [EnableRateLimiting("writes")]
    [HttpPost("password/change")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [AllowAnonymous]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordDTO dto, CancellationToken ct)
    {
        var res = await _accountService.ChangePassword(dto, ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("password-email")]
    [HttpPost("password/request")]
    [RequireCaptcha("password_reset_request")]
    [AllowAnonymous]
    public async Task<ActionResult> RequestChangePassword([FromBody] string email, CancellationToken ct)
    {
        var res = await _accountService.RequestPasswordReset(email, ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("register")]
    [HttpPost("register")]
    [RequireCaptcha("user_register")]
    public async Task<ActionResult<RegisterResponseDTO>> Register(RegisterInfo credentials)
    {
        var res = await _accountService.Register(credentials);
        return GetActionResult(res);
    }
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthenticationResponse>> Login(LoginCredentials credentials)
    {
        var res = await _accountService.Login(credentials);
        return res == null ? 
            BadRequest() : 
            res;
    }
    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult> Logout()
    {
        var res = await _accountService.Logout();
        return res ?
            Ok() :
            BadRequest();
    }
    [HttpGet("refresh-token")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("user_refreshToken")]
    public async Task<ActionResult<AuthenticationResponse>> RefreshToken()
    {
        var res = await _accountService.RefreshToken();
        return res == null ? 
            BadRequest() : 
            res;
    }
    [EnableRateLimiting("writes")]
    [HttpPost("add-role")]
    [Authorize(AuthenticationSchemes =JwtBearerDefaults.AuthenticationScheme, Policy = "Admin")]
    public async Task<ActionResult> AddRole(EditRole editRole)
    {
        return await _accountService.AddRole(editRole) ?
            NoContent() :
            BadRequest();
    }
    [EnableRateLimiting("writes")]
    [HttpPost("remove-role")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "Admin")]
    public async Task<ActionResult> RemoveRole(EditRole editRole)
    {
        return await _accountService.RemoveRole(editRole) ?
            NoContent() :
            BadRequest(); 
    }

    internal async Task<IActionResult> ConfirmUser(string userId, string token)
    {
        return await _accountService.ConfirmUser(userId, token) switch
        {
            AccountServiceResult.UserNotFound => NotFound(),
            AccountServiceResult.InvalidToken => BadRequest(),
            _ => Ok(),
        };
    }
    [NonAction]
    public ObjectResult GetActionResult(AccountServiceResult result)
    {
        string res = $"{GetType().Name}-{result}";

        return result switch
        {
            AccountServiceResult.Success => Ok(res),

            AccountServiceResult.UserNotFound => NotFound(res),

            AccountServiceResult.UserAlreadyExists => Conflict(res),

            AccountServiceResult.InvalidEmail or
            AccountServiceResult.InvalidPassword or
            AccountServiceResult.InvalidToken or
            AccountServiceResult.PasswordChangeError or
            AccountServiceResult.UserCreationError => BadRequest(res),

            AccountServiceResult.CancelledToken => StatusCode(499, res),

            AccountServiceResult.AvatarLoadError or
            AccountServiceResult.TokenCreationError or
            AccountServiceResult.DBError or
            AccountServiceResult.Error =>
                StatusCode(StatusCodes.Status500InternalServerError, res),

            _ => StatusCode(StatusCodes.Status500InternalServerError, res)
        };
    }
    [NonAction]
    public ObjectResult GetActionResult(OneOf<AccountServiceResult, EmailServiceResult> result)
    {
        return result.TryPickT0(out var error1, out var error2) ?
            GetActionResult(error1) :
            _emailController.GetActionResult(error2);
    }
    [NonAction]
    public ObjectResult GetActionResult(OneOf<AccountServiceResult, FileServiceResult> result)
    {
        return result.TryPickT0(out var error1, out var error2) ?
            GetActionResult(error1) :
            _fileController.GetActionResult(error2);
    }
    [NonAction]
    public ActionResult<T> GetActionResult<T>(OneOf<T, AccountServiceResult, FileServiceResult> result)
    {
        return result.TryPickT0(out var res, out var error) ?
                    res :
                    error.TryPickT0(out var error1, out var error2) ?
                        GetActionResult(error1) :
                        _fileController.GetActionResult(error2);
    }
}
```

## RegisterController.cs

Archivo destino: `SNEStorage/Controllers/RegisterController.cs`.

Cambios pendientes incluidos:

- `Register`: `register`.

```csharp
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNEStorage.DTOs;
using SNEStorage.Filters.Attributes;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/register")]
public class RegisterController(AccountController accountController,
    CacheService cacheService) : Controller
{
    private readonly AccountController _accountController = accountController;
    private readonly CacheService _cacheService = cacheService;
    [HttpGet("options")]
    public async Task<ActionResult<RegistrationOptionsDto>> GetRegistrationOptions(CancellationToken ct)
    {
        var timezones = await _cacheService.GetAllTimeZones(ct);
        if (timezones == null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        var visibilities = await _cacheService.GetAllVisibilities(ct);
        if (visibilities == null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        RegistrationOptionsDto options = new()
        {
            TimeZones = [..timezones
                .OrderBy(kvp => kvp.Value.Id)
                .Select(kvp => new IdNamePair
                {
                    Id = kvp.Value.Id,
                    Name = kvp.Value.Name
                })],
            EmailVisibilities = [..visibilities
                .Where(kvp => !kvp.Value.ResourceOnly)
                .OrderBy(kvp => kvp.Value.Id)
                .Select(kvp => new IdNamePair
                {
                    Id = kvp.Value.Id,
                    Name = kvp.Value.Name!
                })]
        };

        return Ok(options);
    }
    [EnableRateLimiting("register")]
    [HttpPost("submit")]
    [ValidateAntiForgeryToken]
    [RequireCaptcha("user_register")]
    public async Task<ActionResult<RegisterResponseDTO>> Register([FromForm] RegisterInfo credentials)
    {
        return await _accountController.Register(credentials);
    }
    [AllowAnonymous]
    [HttpPost("confirmed/{userId}")]
    public async Task<IActionResult> Confirmed([FromRoute] string userId, [FromQuery] string token)
    {
        return await _accountController.ConfirmUser(userId, token);
    }
}
```

## ResourceController.cs

Archivo destino: `SNEStorage/Controllers/ResourceController.cs`.

Cambios pendientes incluidos:

- `Find`: `reads`.
- `AddComment`: `comments`.
- `AddOrEditScore`: `votes`.
- `GetCreateOptions`: `reads`.
- `Get`: `reads`.
- `Edit`: `uploads`.
- `EditVersion`: `writes`.
- `GetPreviews`: `reads`.
- `GetFile`: `reads`.
- `Download`: `reads`.
- `GetResourcesLike`: `reads`.
- `CreateResource`: `uploads`.
- `CreateVersion`: `uploads`.
- `GetDependencies`: `reads`.
- `GetExternalDependencies`: `reads`.
- `GetAuthors`: `reads`.
- `GetExternalAuthors`: `reads`.
- `GetVersions`: `reads`.
- `GetLatestVersion`: `reads`.
- `GetRating`: `reads`.
- Comentarios: validación máxima de 5.000 caracteres en los parámetros HTTP.

```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Filters.Attributes;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/resource")]
public class ResourceController(SnestorageContext context,
    CacheService cacheService,
    ResourceService resourceService,
    FileController fileController,
    GetLikeController getLikeController,
    IWebHostEnvironment Environment) : Controller
{
    private readonly SnestorageContext _context = context;
    private readonly CacheService _cacheService = cacheService;
    private readonly ResourceService _resourceService = resourceService;
    private readonly FileController _fileController = fileController;
    private readonly GetLikeController _getLikeController = getLikeController;
    private readonly IWebHostEnvironment _environment = Environment;
    [EnableRateLimiting("reads")]
    [HttpGet("find")]
    public async Task<ActionResult<List<ResourceFinderViewDTO>>> Find([FromQuery] ResourceFinderDTO dto, 
        [FromQuery] int maxAmount, [FromQuery] int page, CancellationToken ct = default)
    {
        var result = await _resourceService.Find(dto, maxAmount, page, ct);
        return GetActionResult(result);
    }
    [EnableRateLimiting("comments")]
    [HttpPost("{resourceId}/Comment/Submit")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("resource_addComment", CaptchaPolicy.After5TriesInShortTime)]
    public async Task<ActionResult<ResourceComments>> AddComment([FromRoute] long resourceId, [FromForm, StringLength(5000)] string commentText, CancellationToken ct)
    {
        var res = await _resourceService.AddComment(resourceId, commentText, ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("votes")]
    [HttpPost("{resourceId}/Score/Submit/{value}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("resource_addOrEditScore", CaptchaPolicy.After10TriesInShortTime)]
    public async Task<ActionResult<ScoreUsers>> AddOrEditScore([FromRoute] long resourceId, [FromRoute] long value, CancellationToken ct)
    {
        var res = await _resourceService.AddOrEditScore(resourceId, value, ct);
        return GetActionResult(res);
    }

    [EnableRateLimiting("reads")]
    [HttpGet("options")]
    public async Task<ActionResult<ResourceCreateOptionsDto>> GetCreateOptions(CancellationToken ct)
    {
        var resourceTypes = await _cacheService.GetAllTypes(ct);
        if (resourceTypes == null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        var visibilities = await _cacheService.GetAllVisibilities(ct);
        if (visibilities == null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        var options = new ResourceCreateOptionsDto
        {
            ResourceTypes = [..resourceTypes
                .OrderBy(r => r.Value.Name)
                .Select(r => new ResourceTypeDTO
                {
                    Id = r.Value.Id,
                    Name = r.Value.Name!,
                    CategoryId = r.Value.CategoryId,
                    CategoryName = r.Value.Category!.Name
                })],
            Visibilities = [..visibilities
                .OrderBy(v => v.Value.Id)
                .Select(v => new IdNamePair
                {
                    Id = v.Value.Id,
                    Name = v.Value.Name!
                })]
        };

        return Ok(options);
    }
    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId}")]
    public async Task<ActionResult<ResourceReadDTO>> Get([FromRoute]long resourceId, CancellationToken ct = default)
    {
        var res = await _resourceService.Get(resourceId, ct: ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("uploads")]
    [HttpPut("{resourceId}/edit")]
    [RequestSizeLimit(110L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5L * 1024 * 1024)]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("resource_edit")]
    public async Task<ActionResult> Edit([FromRoute] long resourceId, [FromForm] ResourceEditDTO dto, CancellationToken ct = default)
    {
        var res = await _resourceService.Edit(resourceId, dto, ct: ct);

        return GetActionResult(res);
    }
    [EnableRateLimiting("writes")]
    [HttpPut("{resourceId}/version/{versionId}/edit")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("resource_editVersion")]
    public async Task<ActionResult> EditVersion([FromRoute] long resourceId, [FromRoute] long versionId, [FromForm] ResourceVersionEditDTO dto, CancellationToken ct = default)
    {
        var res = await _resourceService.EditVersion(resourceId, versionId, dto, ct: ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId:long}/previews")]
    public async Task<ActionResult<List<ResourcePreview>>> GetPreviews([FromRoute] long resourceId, CancellationToken ct = default)
    {
        var res = await _resourceService.GetResourceAllPreviews(resourceId, ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId:long}/file/{resourceFileId:long}")]
    public async Task<ActionResult<Models.File>> GetFile([FromRoute] long resourceId, [FromRoute] long resourceFileId, CancellationToken ct = default)
    {
        var res = await _resourceService.GetFile(resourceId, resourceFileId, ct: ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId}/file/{resourceFileId}/download")]
    public async Task<ActionResult> Download([FromRoute] long resourceId, [FromRoute] long resourceFileId, CancellationToken ct = default)
    {
        var res = await _resourceService.Download(resourceId, resourceFileId, ct);
        if (res.TryPickT0(out var file, out var error))
            return PhysicalFile(file.Item1, file.Item2, Path.GetFileName(file.Item1), true);
        return GetActionResult(error);
    }
    [EnableRateLimiting("reads")]
    [HttpGet("search")]
    public async Task<ActionResult<List<Resource>>> GetResourcesLike([FromQuery] string likeString, [FromQuery] int maxAmount = 10, CancellationToken ct = default)
    {
        var resources = await _resourceService.GetResourceLike(likeString, maxAmount, ct);
        return _getLikeController.GetActionResult(resources);
    }
    [EnableRateLimiting("uploads")]
    [HttpPost("create/submit")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequestSizeLimit(360L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 250L * 1024 * 1024)]
    [RequireCaptcha("resource_create")]
    public async Task<ActionResult<Resource>> CreateResource([FromForm] ResourceCreateDTO dto, CancellationToken ct)
    {
        var result = await _resourceService.Create(dto, ct);
        return GetActionResult(result);
    }
    [EnableRateLimiting("uploads")]
    [HttpPost("{resourceId}/version/create/submit")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequestSizeLimit(360L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 250L * 1024 * 1024)]
    [RequireCaptcha("resource_createVersion")]
    public async Task<ActionResult<ResourceVersion>> CreateVersion([FromRoute] long resourceId, [FromForm] ResourceVersionCreateDTO dto, CancellationToken ct)
    {
        var result = await _resourceService.AddVersion(resourceId, dto, ct);
        return GetActionResult(result);
    }
    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId:long}/dependencies")]
    public async Task<ActionResult<List<ResourceDependencyViewDTO>>> GetDependencies([FromRoute] long resourceId, CancellationToken ct = default)
    {
        var result = await _resourceService.GetResourceAllDependencies(resourceId, ct);
        return GetActionResult(result);
    }

    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId:long}/external-dependencies")]
    public async Task<ActionResult<List<ResourceExternalDependency>>> GetExternalDependencies([FromRoute] long resourceId, CancellationToken ct = default)
    {
        var result = await _resourceService.GetResourceAllExternalDependencies(resourceId, ct);
        return GetActionResult(result);
    }

    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId:long}/authors")]
    public async Task<ActionResult<List<ResourceAuthorDTO>>> GetAuthors([FromRoute] long resourceId, CancellationToken ct = default)
    {
        var result = await _resourceService.GetResourceAllAuthors(resourceId, ct);
        return GetActionResult(result);
    }

    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId:long}/external-authors")]
    public async Task<ActionResult<List<ResourceExternalAuthor>>> GetExternalAuthors([FromRoute] long resourceId, CancellationToken ct = default)
    {
        var result = await _resourceService.GetResourceAllExternalAuthors(resourceId, ct);
        return GetActionResult(result);
    }

    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId:long}/versions")]
    public async Task<ActionResult<List<ResourceVersion>>> GetVersions([FromRoute] long resourceId, CancellationToken ct = default)
    {
        var result = await _resourceService.GetResourceAllVersion(resourceId, ct);
        return GetActionResult(result);
    }

    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId:long}/versions/latest")]
    public async Task<ActionResult<ResourceVersion>> GetLatestVersion([FromRoute] long resourceId, CancellationToken ct = default)
    {
        var result = await _resourceService.GetResourceLastVersion(resourceId, ct);
        return GetActionResult(result);
    }

    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId:long}/rating")]
    public async Task<ActionResult<RatingReadDto>> GetRating([FromRoute] long resourceId, CancellationToken ct = default)
    {
        var result = await _resourceService.GetRating(resourceId, ct);
        return GetActionResult(result);
    }
    private ObjectResult GetActionResult(ResourceServiceResult result)
    {
        string res = $"{GetType().Name}-{result}";
        bool authenticated =
            HttpContext?.User.Identity?.IsAuthenticated == true;

        return result switch
        {
            ResourceServiceResult.Success => Ok(res),

            ResourceServiceResult.FileNotFound or
            ResourceServiceResult.ResourceNotFound or
            ResourceServiceResult.ResourceTypeNotFound or
            ResourceServiceResult.VersionNotFound =>
                NotFound(res),

            ResourceServiceResult.Unauthorized when authenticated =>
                StatusCode(StatusCodes.Status403Forbidden, res),

            ResourceServiceResult.Unauthorized =>
                Unauthorized(res),

            ResourceServiceResult.UserNotFound when authenticated =>
                BadRequest(res),

            ResourceServiceResult.UserNotFound =>
                Unauthorized(res),

            ResourceServiceResult.CantAddScoreToPrivateResource or
            ResourceServiceResult.CantScoreSelfResource =>
                StatusCode(StatusCodes.Status403Forbidden, res),

            ResourceServiceResult.VersionAlreadyExists =>
                Conflict(res),

            ResourceServiceResult.PreviewSizeSurpassTheLimit or
            ResourceServiceResult.FileSizeSurpassTheLimit =>
                StatusCode(StatusCodes.Status413PayloadTooLarge, res),

            ResourceServiceResult.FileCreationError or
            ResourceServiceResult.BatchCreationError or
            ResourceServiceResult.FileDeletionError or
            ResourceServiceResult.DBError or
            ResourceServiceResult.CacheError =>
                StatusCode(StatusCodes.Status500InternalServerError, res),

            ResourceServiceResult.InvalidMaxAmount or
            ResourceServiceResult.SkipOutOfRange or
            ResourceServiceResult.MaxAmountSurpassTheLimit or
            ResourceServiceResult.NegativePage or
            ResourceServiceResult.InvalidScore or
            ResourceServiceResult.EmptyComment or
            ResourceServiceResult.PreviewAmountSurpassTheLimit or
            ResourceServiceResult.EmptyName or
            ResourceServiceResult.EmptyDescription or
            ResourceServiceResult.InvalidVersion or
            ResourceServiceResult.InvalidMaxAmount or
            ResourceServiceResult.InvalidName or
            ResourceServiceResult.InvalidRepositoryURL or
            ResourceServiceResult.InvalidDescription or
            ResourceServiceResult.AmountOfElementsSurpassTheLimit or
            ResourceServiceResult.InvalidReleaseNote or
            ResourceServiceResult.InvalidSort or
            ResourceServiceResult.InvalidScoreRange or
            ResourceServiceResult.InvalidDateRange or
            ResourceServiceResult.EmptyTypeList =>
                BadRequest(res),

            _ => StatusCode(StatusCodes.Status500InternalServerError, res)
        };
    }
    private ActionResult<T> GetActionResult<T>(OneOf<T, ResourceServiceResult> oneof)
    {
        return oneof.TryPickT0(out var item, out var error) ?
            Ok(item) :
            GetActionResult(error);
    }
    private ActionResult<T> GetActionResult<T>(OneOf<T, ResourceServiceResult, FileServiceResult> oneof)
    {
        return oneof.TryPickT0(out var item, out var error) ?
            Ok(item) :
            error.TryPickT0(out var error1, out var error2) ?
                GetActionResult(error1) :
                _fileController.GetActionResult(error2);
    }
    private ActionResult GetActionResult(OneOf<ResourceServiceResult, FileServiceResult> oneof)
    {
        return oneof.TryPickT0(out var res1, out var res2) ?
            GetActionResult(res1) :
            _fileController.GetActionResult(res2);
    }
}
```

## ProfileController.cs

Archivo destino: `SNEStorage/Controllers/ProfileController.cs`.

Cambios pendientes incluidos:

- `EditMyProfile`: `uploads`.

```csharp
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/profile")]
[Authorize]
public class ProfileController(AccountService accountService) : ControllerBase
{
    private readonly AccountService _accountService = accountService;
    [HttpGet("me")]
    [ProducesResponseType<UserInfo>(StatusCodes.Status200OK)]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<UserInfo?>> GetMyProfile(CancellationToken ct)
    {
        var result = await _accountService.GetUserInfo(ct);
        if (result.IsT0)
            return Ok(result.AsT0);
        return result.AsT1 switch
        {
            AccountServiceResult.UserNotFound => NotFound(),
            AccountServiceResult.CancelledToken => StatusCode(StatusCodes.Status499ClientClosedRequest),
            _ => BadRequest(),
        };
    }
    [EnableRateLimiting("uploads")]
    [HttpPut("me/edit")]
    [Consumes("multipart/form-data")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult> EditMyProfile([FromForm] EditProfileDTO dto, CancellationToken ct)
    {
        return await _accountService.EditUserInfo(dto, ct) ? Ok() : BadRequest();
    }
}
```

## CommentController.cs

Archivo destino: `SNEStorage/Controllers/CommentController.cs`.

Cambios pendientes incluidos:

- `GetUserLike`: `reads`.
- `AddEditOrDeleteLike`: `votes`.
- `GetReplies`: `reads`.
- `GetResourceComments`: `reads`.
- `Reply`: `comments`.
- `Delete`: `comments`.
- `Edit`: `comments`.
- Comentarios: validación máxima de 5.000 caracteres en los parámetros HTTP.

```csharp
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Filters.Attributes;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/comment")]
public class CommentController(CommentService commentService,
    DBHelperService dbHelperService) : Controller
{
    private readonly CommentService _commentService = commentService;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    [EnableRateLimiting("reads")]
    [HttpGet("{commentId}/userlike")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<LikeUsers>> GetUserLike([FromRoute] long commentId, CancellationToken ct = default)
    {
        var res = await _commentService.GetUserLike(commentId, ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("votes")]
    [HttpPost("{commentId}/like")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("comment_AddEditOrDeleteLike", CaptchaPolicy.After10TriesInShortTime)]
    public async Task<ActionResult<LikeUsers>> AddEditOrDeleteLike([FromRoute] long commentId,
        [FromForm] LikeValues value, CancellationToken ct = default)
    {
        var res = await _commentService.AddEditOrDeleteLike(commentId, value, ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("reads")]
    [HttpGet("{commentId}/replies")]
    public async Task<ActionResult<List<CommentReadDto>>> GetReplies([FromRoute] long commentId,
        [FromQuery] int maxAmount = 20, [FromQuery] int page = 0,
        [FromQuery] CommentSorting sort = CommentSorting.RecentFirst,
    CancellationToken ct = default)
    {
        var res = await _commentService.GetReplies(commentId, maxAmount, page, sort, ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("reads")]
    [HttpGet("resource/{resourceId}")]
    public async Task<ActionResult<List<CommentReadDto>>> GetResourceComments([FromRoute] long resourceId,
        [FromQuery] int maxAmount = 20, [FromQuery] int page = 0,
        [FromQuery] CommentSorting sort = CommentSorting.RecentFirst,
        CancellationToken ct = default)
    {
        var res = await _commentService.GetResourceComments(resourceId, maxAmount, page, sort, ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("comments")]
    [HttpPost("{commentId}/reply")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("comment_reply", CaptchaPolicy.After5TriesInShortTime)]
    public async Task<ActionResult<CommentComment>> Reply([FromRoute] long commentId, [FromForm, StringLength(5000)] string commentText, CancellationToken ct = default)
    {
        var res = await _commentService.AddCommentToComment(commentId, commentText, ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("comments")]
    [HttpDelete("{commentId}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult> Delete([FromRoute] long commentId, CancellationToken ct = default)
    {
        var res = await _commentService.DeleteComment(commentId, ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("comments")]
    [HttpPut("{commentId}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("comment_edit", CaptchaPolicy.After5TriesInShortTime)]
    public async Task<ActionResult> Edit([FromRoute] long commentId, [FromForm, StringLength(5000)] string commentText, CancellationToken ct = default)
    {
        var res = await _commentService.EditComment(commentId, commentText, ct);
        return GetActionResult(res);
    }
    private ObjectResult GetActionResult(CommentServiceResult result)
    {
        string res = $"{GetType().Name}-{result}";
        bool authenticated =
            HttpContext?.User.Identity?.IsAuthenticated == true;

        return result switch
        {
            CommentServiceResult.Success => Ok(res),

            CommentServiceResult.Unauthorized when authenticated =>
                StatusCode(StatusCodes.Status403Forbidden, res),

            CommentServiceResult.Unauthorized =>
                Unauthorized(res),

            CommentServiceResult.CommentNotFound or
            CommentServiceResult.ResourceNotFound or
            CommentServiceResult.LikeNotFound =>
                NotFound(res),

            CommentServiceResult.CantLikeYourOwnComment =>
                StatusCode(StatusCodes.Status403Forbidden, res),

            CommentServiceResult.CantLikeDeletedComment or
            CommentServiceResult.CantReplyDeletedComment =>
                Conflict(res),

            CommentServiceResult.UndefinedLike or
            CommentServiceResult.UndefinedSort or
            CommentServiceResult.AmountOfCommentsSurpassTheLimit or
            CommentServiceResult.InvalidAmountOfCommentsPerPage or
            CommentServiceResult.NegativePage or
            CommentServiceResult.SkipOutOfRange or
            CommentServiceResult.EmptyComment =>
                BadRequest(res),

            CommentServiceResult.DBError =>
                StatusCode(StatusCodes.Status500InternalServerError, res),

            _ => StatusCode(StatusCodes.Status500InternalServerError, res)
        };
    }
    private ActionResult<T> GetActionResult<T>(OneOf<T, CommentServiceResult> result)
    {
        if (result.TryPickT0(out T entity, out var error))
            return Ok(entity);
        return GetActionResult(error);
    }
}
```

## NewsController.cs

Archivo destino: `SNEStorage/Controllers/NewsController.cs`.

Cambios pendientes incluidos:

- `FullGet`: `reads`.
- `Create`: `writes`.
- `Edit`: `writes`.

```csharp
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/news")]
public class NewsController(NewsService newsService) : Controller
{
    private readonly NewsService _newsService = newsService;

    [EnableRateLimiting("reads")]
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<NewsReadDTO>>> FullGet([FromQuery] NewsFilterDTO filter,
        [FromQuery] NewsSorting sort = NewsSorting.RecentFirst, [FromQuery] int maxAmount = 10,
        [FromQuery] int page = 0, CancellationToken ct = default)
    {
        var res = await _newsService.FullGet(
            filter, sort, maxAmount, page, ct);

        return GetActionResult(res);
    }
    [EnableRateLimiting("writes")]
    [HttpPost("create")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<long>> Create([FromBody] NewsCreateDTO dto, 
        CancellationToken ct = default)
    {
        var res = await _newsService.Create(dto, ct);

        return res.TryPickT0(out var news, out var error) ?
            Ok(news.Id) : GetActionResult(error);
    }
    [EnableRateLimiting("writes")]
    [HttpPut("{newsId:long}/edit")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<long>> Edit([FromRoute] long newsId, [FromBody] NewsEditDTO dto,
        CancellationToken ct = default)
    {
        var res = await _newsService.Edit(newsId, dto, ct);

        return res.TryPickT0(out var news, out var error) ?
            Ok(news.Id) : GetActionResult(error);
    }
    private ObjectResult GetActionResult(NewsServiceResult result)
    {
        string res = $"{GetType().Name}-{result}";
        bool authenticated =
            HttpContext?.User.Identity?.IsAuthenticated == true;

        return result switch
        {
            NewsServiceResult.Success => Ok(res),

            NewsServiceResult.Unauthorized when authenticated =>
                StatusCode(StatusCodes.Status403Forbidden, res),

            NewsServiceResult.Unauthorized =>
                Unauthorized(res),

            NewsServiceResult.UserNotFound or
            NewsServiceResult.NewNotFound =>
                NotFound(res),

            NewsServiceResult.InvalidTitle or
            NewsServiceResult.InvalidText or
            NewsServiceResult.InvalidSort or
            NewsServiceResult.InvalidMaxAmount or
            NewsServiceResult.InvalidPage or
            NewsServiceResult.SkipOutOfRange =>
                BadRequest(res),

            NewsServiceResult.DBError =>
                StatusCode(StatusCodes.Status500InternalServerError, res),

            _ => StatusCode(StatusCodes.Status500InternalServerError, res)
        };
    }
    private ActionResult<T> GetActionResult<T>(
        OneOf<T, NewsServiceResult> result)
    {
        return result.TryPickT0(out var entity, out var error) ?
            Ok(entity) : GetActionResult(error);
    }
}
```

## UserController.cs

Archivo destino: `SNEStorage/Controllers/UserController.cs`.

Cambios pendientes incluidos:

- `GetUsersLike`: `reads`.

```csharp
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using SNEStorage.DTOs;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/user")]
public class UserController(UserService userService,
    GetLikeController getLikeController) : Controller
{
    private readonly UserService _userService = userService;
    private readonly GetLikeController _getLikeController = getLikeController;
    [EnableRateLimiting("reads")]
    [HttpGet("search")]
    public async Task<ActionResult<List<UserNameDTO>>> GetUsersLike([FromQuery] string likeString, [FromQuery] int maxAmount = 10, CancellationToken ct = default)
    {
        var users = await _userService.GetUserLike(likeString, maxAmount, ct);
        return _getLikeController.GetActionResult(users);
    }
}
```

## VideogameController.cs

Archivo destino: `SNEStorage/Controllers/VideogameController.cs`.

Cambios pendientes incluidos:

- `GetVideogamesLike`: `reads`.

```csharp
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/videogames")]
public class VideogameController(VideogameService videogameService,
    GetLikeController getLikeController) : Controller
{
    private readonly VideogameService _videogameService = videogameService;
    private readonly GetLikeController _getLikeController = getLikeController;
    [EnableRateLimiting("reads")]
    [HttpGet("search")]
    public async Task<ActionResult<List<Videogame>>> GetVideogamesLike([FromQuery] string? likeString = null, [FromQuery] int maxAmount = 10, CancellationToken ct = default)
    {
        var videogames = await _videogameService.GetVideogamesLike(likeString?? string.Empty, maxAmount, ct);
        return _getLikeController.GetActionResult(videogames);
    }
}
```

## ExternalAuthorController.cs

Archivo destino: `SNEStorage/Controllers/ExternalAuthorController.cs`.

Cambios pendientes incluidos:

- `GetExternalAuthorsLike`: `reads`.

```csharp
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/externa-author")]
public class ExternalAuthorController(ExternalAuthorService externalAuthorService,
    GetLikeController getLikeController) : Controller
{
    private readonly ExternalAuthorService _externalAuthorService = externalAuthorService;
    private readonly GetLikeController _getLikeController = getLikeController;
    [EnableRateLimiting("reads")]
    [HttpGet]
    public async Task<ActionResult<List<string>>> GetExternalAuthorsLike([FromQuery] string likeString, [FromQuery] int maxAmount = 10, CancellationToken ct = default)
    {
        var externalAuthors = await _externalAuthorService.GetAuthorLike(likeString, maxAmount, ct);
        return _getLikeController.GetActionResult(externalAuthors);
    }
}
```

## EditProfileDTO.cs

Archivo destino: `SNEStorage/DTOs/EditProfileDTO.cs`.

Cambio pendiente: máximo de 2.000 caracteres para Bio; null sigue significando sin cambios.

```csharp
using System.ComponentModel.DataAnnotations;

namespace SNEStorage.DTOs;
public class EditProfileDTO
{
    public string? UsernameColor { get; set; }
    public IFormFile? File { get; set; }
    [StringLength(2000)]
    public string? Bio { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Patreon { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Paypal { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? BuyMeACoffee { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Discord { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Twitter { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Facebook { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Youtube { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Instagram { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Steam { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Twitch { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Github { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? NintendoNetwork { get; set; }
    public long? EmailVisibilityId { get; set; }
    public long? TimeZoneId { get; set; }
}
```

## Después de aplicar

- Los atributos activan las políticas que ya registraste en Program.cs. El cupo se comparte entre las acciones de la misma política y clave; las llamadas directas entre controladores no cuentan como una segunda petición HTTP.
- Los tres parámetros de comentarios y Bio reciben validación de modelo por ApiController. Sus límites no necesitan una migración de base de datos.
- El frontend ya limita comentarios a 5.000 y Bio a 2.000, presenta los errores 429 de las APIs y reconoce el mensaje de login rate-limited.
- El límite global de dos peticiones concurrentes se aplica a las acciones que aquí reciben la política uploads.

El documento fue contrastado con los archivos originales para conservar sus implementaciones. No se ejecutaron pruebas HTTP ni se compiló esta propuesta como sustitución del backend.
