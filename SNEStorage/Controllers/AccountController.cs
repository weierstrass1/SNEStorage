using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNEStorage.DTOs;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController(AccountService accountService) : ControllerBase
{
    public AccountService AccountService { get; } = accountService;

    [HttpPost("register")]
    public async Task<ActionResult<RegisterResponseDTO>> Register(RegisterInfo credentials)
    {
        var res = await AccountService.Register(credentials);

        if (res.TryPickT0(out var response, out var error))
            return response;

        return BadRequest(error);
    }
    [HttpPost("login")]
    public async Task<ActionResult<AuthenticationResponse>> Login(LoginCredentials credentials)
    {
        var res = await AccountService.Login(credentials);
        return res == null ? 
            BadRequest() : 
            res;
    }
    [HttpPost("logout")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult> Logout()
    {
        var res = await AccountService.Logout();
        return res ?
            Ok() :
            BadRequest();
    }
    [HttpGet("refresh-token")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<AuthenticationResponse>> RefreshToken()
    {
        var res = await AccountService.RefreshToken();
        return res == null ? 
            BadRequest() : 
            res;
    }
    [HttpPost("add-role")]
    [Authorize(AuthenticationSchemes =JwtBearerDefaults.AuthenticationScheme, Policy = "Admin")]
    public async Task<ActionResult> AddRole(EditRole editRole)
    {
        return await AccountService.AddRole(editRole) ?
            NoContent() :
            BadRequest();
    }
    [HttpPost("remove-role")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "Admin")]
    public async Task<ActionResult> RemoveRole(EditRole editRole)
    {
        return await AccountService.RemoveRole(editRole) ?
            NoContent() :
            BadRequest(); 
    }

    internal async Task<IActionResult> ConfirmUser(string userId, string token)
    {
        return await AccountService.ConfirmUser(userId, token) switch
        {
            AccountServiceResult.UserNotFound => NotFound(),
            AccountServiceResult.InvalidToken => BadRequest(),
            _ => Ok(),
        };
    }
}
