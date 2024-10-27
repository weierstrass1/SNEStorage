using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNEStorage.DTOs;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    public AccountService AccountService { get; }

    public AccountController(AccountService accountService)
    {
        AccountService = accountService;
    }
    [HttpPost("register")]
    public async Task<ActionResult<AuthenticationResponse>> Register(RegisterInfo credentials)
    {
        var res = await AccountService.Register(credentials);
        return res == null ? 
            BadRequest() : 
            res;
    }
    [HttpPost("login")]
    public async Task<ActionResult<AuthenticationResponse>> Login(LoginCredentials credentials)
    {
        var res = await AccountService.Login(credentials);
        return res == null ? 
            BadRequest() : 
            res;
    }
    [HttpGet("refreshToken")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<AuthenticationResponse>> RefreshToken()
    {
        var res = await AccountService.RefreshToken();
        return res == null ? 
            BadRequest() : 
            res;
    }
    [HttpPost("AddRole")]
    [Authorize(AuthenticationSchemes =JwtBearerDefaults.AuthenticationScheme, Policy = "Admin")]
    public async Task<ActionResult> AddRole(EditRole editRole)
    {
        var res = await AccountService.AddRole(editRole);
        return res == null ? 
            BadRequest() : 
            NoContent();
    }
    [HttpPost("RemoveRole")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "Admin")]
    public async Task<ActionResult> RemoveRole(EditRole editRole)
    {
        var res = await AccountService.RemoveRole(editRole);
        return res == null ? 
            BadRequest() : 
            NoContent();
    }
}
