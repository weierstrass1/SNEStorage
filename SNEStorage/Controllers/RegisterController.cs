using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNEStorage.DTOs;
using SNEStorage.Filters.Attributes;
using SNEStorage.Services;
using System.ComponentModel.DataAnnotations;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/register")]
public class RegisterController(AccountService accountService, CacheService cacheService) : Controller
{
    private readonly AccountService _accountService = accountService;
    private readonly CacheService _cacheService = cacheService;

    [HttpGet("options")]
    public async Task<ActionResult<RegistrationOptionsDto>> GetRegistrationOptions(CancellationToken ct)
    {
        var timezones = await _cacheService.GetAllTimeZones(ct);
        if (!timezones.TryPickT0(out var timezoneValues, out var timezoneError))
            return this.GetActionResult(timezoneError);

        var visibilities = await _cacheService.GetAllVisibilities(ct);
        if (!visibilities.TryPickT0(out var visibilityValues, out var visibilityError))
            return this.GetActionResult(visibilityError);

        RegistrationOptionsDto options = new()
        {
            TimeZones = [..timezoneValues
                .OrderBy(kvp => kvp.Value.Id)
                .Select(kvp => new IdNamePair
                {
                    Id = kvp.Value.Id,
                    Name = kvp.Value.Name
                })],
            EmailVisibilities = [..visibilityValues
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
        var result = await _accountService.Register(credentials);
        return this.GetActionResult(result);
    }
    [AllowAnonymous]
    [HttpPost("confirmed/{userId}")]
    public async Task<IActionResult> Confirmed([FromRoute, StringLength(TextLimits.USER_ID)] string userId,
        [FromQuery, StringLength(TextLimits.TOKEN)] string token, CancellationToken ct = default)
    {
        var result = await _accountService.ConfirmUser(userId, token, ct);
        return this.GetActionResult(result);
    }
}
