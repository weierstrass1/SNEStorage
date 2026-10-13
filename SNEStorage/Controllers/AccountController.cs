using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Filters.Attributes;
using SNEStorage.Services;
using System.ComponentModel.DataAnnotations;

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
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "Admin")]
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
    internal async Task<ActionResult> ConfirmUser(string userId, string token, 
        CancellationToken ct)
    {
        var res = await _accountService.ConfirmUser(userId, token, ct);
        return GetActionResult(res);
    }
    [NonAction]
    public ActionResult<T> GetActionResult<T>(OneOf<T,AccountServiceResult> result)
    {
        return result.TryPickT0(out var item, out var error) ?
            Ok(item) :
            GetActionResult(error);
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
