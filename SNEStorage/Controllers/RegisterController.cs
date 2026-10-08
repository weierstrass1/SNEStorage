using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/register")]
public class RegisterController(AccountController accountController,
    SnestorageContext context) : Controller
{
    public AccountController AccountController { get; } = accountController;
    public SnestorageContext Context { get; } = context;
    [HttpGet("options")]
    public async Task<ActionResult<RegistrationOptionsDto>> GetRegistrationOptions(CancellationToken ct)
    {
        var options = new RegistrationOptionsDto
        {
            TimeZones = await Context.TimeZones.AsNoTracking()
                .OrderBy(timeZone => timeZone.Id)
                .Select(timeZone => new SelectOptionDto { Id = timeZone.Id, Name = timeZone.Name })
                .ToListAsync(ct),
            EmailVisibilities = await Context.Visibilities.AsNoTracking()
                .Where(visibility => !visibility.ResourceOnly)
                .OrderBy(visibility => visibility.Id)
                .Select(visibility => new SelectOptionDto { Id = visibility.Id, Name = visibility.Name })
                .ToListAsync(ct)
        };

        return Ok(options);
    }
    [HttpPost("submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register([FromForm] RegisterInfo credentials)
    {
        var response = await AccountController.Register(credentials);
        return response.Value is null
            ? Redirect("/Register?result=failed")
            : Redirect("/Register/CheckEmail");
    }
    [AllowAnonymous]
    [HttpPost("confirmed/{userId}")]
    public async Task<IActionResult> Confirmed([FromRoute] string userId, [FromQuery] string token, 
        CancellationToken ct)
    {
        return await AccountController.ConfirmUser(userId, token);
    }
}
