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
