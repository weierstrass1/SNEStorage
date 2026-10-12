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
    [AllowAnonymous]
    [HttpGet("{userId}")]
    [ProducesResponseType<UserInfo>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserInfo?>> GetProfile([FromRoute] string userId, CancellationToken ct)
    {
        var result = await _accountService.GetUserInfo(userId, ct);
        if (result.IsT0)
        {
            UserInfo ui = result.AsT0;
            ui.Birthday = DateOnly.MinValue;
            return Ok(result.AsT0);
        }
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
