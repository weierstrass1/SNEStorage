using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SNEStorage.DTOs;
using SNEStorage.Filters.Attributes;
using SNEStorage.Services;
using System.ComponentModel.DataAnnotations;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController(AccountService accountService) : ControllerBase
{
    private readonly AccountService _accountService = accountService;

    [EnableRateLimiting("writes")]
    [HttpPost("password/change")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [AllowAnonymous]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordDTO dto, CancellationToken ct)
    {
        var res = await _accountService.ChangePassword(dto, ct);
        return this.GetActionResult(res);
    }
    [EnableRateLimiting("password-email")]
    [HttpPost("password/request")]
    [RequireCaptcha("password_reset_request")]
    [AllowAnonymous]
    public async Task<ActionResult> RequestChangePassword(
        [FromBody, Required, EmailAddress, StringLength(TextLimits.EMAIL)] string email, CancellationToken ct)
    {
        var res = await _accountService.RequestPasswordReset(email, ct);
        return this.GetActionResult(res);
    }
    [EnableRateLimiting("register")]
    [HttpPost("register")]
    [RequireCaptcha("user_register")]
    public async Task<ActionResult<RegisterResponseDTO>> Register(RegisterInfo credentials)
    {
        var res = await _accountService.Register(credentials);
        return this.GetActionResult(res);
    }
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthenticationResponse>> Login(LoginCredentials credentials)
    {
        var res = await _accountService.Login(credentials);
        return this.GetActionResult(res);
    }
    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult> Logout()
    {
        var res = await _accountService.Logout();
        return this.GetActionResult(res);
    }
    [HttpGet("refresh-token")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("user_refreshToken")]
    public async Task<ActionResult<AuthenticationResponse>> RefreshToken()
    {
        var res = await _accountService.RefreshToken();
        return this.GetActionResult(res);
    }
    [EnableRateLimiting("writes")]
    [HttpPost("add-role")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "Admin")]
    public async Task<ActionResult> AddRole(EditRole editRole)
    {
        var result = await _accountService.AddRole(editRole);
        return this.GetActionResult(result);
    }
    [EnableRateLimiting("writes")]
    [HttpPost("remove-role")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "Admin")]
    public async Task<ActionResult> RemoveRole(EditRole editRole)
    {
        var result = await _accountService.RemoveRole(editRole);
        return this.GetActionResult(result);
    }
    [AllowAnonymous]
    [EnableRateLimiting("password-email")]
    [RequireCaptcha("email_confirmation_resend")]
    [HttpPost("confirmation/resend")]
    public async Task<ActionResult> RequestEmailConfirmation(
        [FromBody, Required, EmailAddress, StringLength(TextLimits.EMAIL)] string email, CancellationToken ct)
    {
        var res = await _accountService.RequestEmailConfirmation(email, ct);
        return this.GetActionResult(res);
    }
}
