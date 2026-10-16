using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;
using System.ComponentModel.DataAnnotations;

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
    public async Task<ActionResult<UserInfo>> GetMyProfile(CancellationToken ct)
    {
        var result = await _accountService.GetUserInfo(ct);
        return this.GetActionResult(result);
    }
    [AllowAnonymous]
    [HttpGet("{userId}")]
    [ProducesResponseType<UserInfo>(StatusCodes.Status200OK)]
    public async Task<ActionResult<UserInfo>> GetProfile(
        [FromRoute, StringLength(TextLimits.USER_ID)] string userId, CancellationToken ct)
    {
        var result = await _accountService.GetUserInfo(userId, ct);
        if (result.IsT0)
        {
            UserInfo ui = result.AsT0;
            ui.Birthday = DateOnly.MinValue;
        }
        return this.GetActionResult(result);
    }
    [EnableRateLimiting("uploads")]
    [HttpPut("me/edit")]
    [Consumes("multipart/form-data")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult> EditMyProfile([FromForm] EditProfileDTO dto, CancellationToken ct)
    {
        var result = await _accountService.EditUserInfo(dto, ct);
        return this.GetActionResult(result);
    }
}
