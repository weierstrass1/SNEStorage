using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SNEStorage.DTOs;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/tools/apply-ips-bps/usage")]
public class ToolController(ToolUsageService toolUsageService) : ControllerBase
{
    private readonly ToolUsageService _toolUsageService = toolUsageService;

    [HttpGet]
    [AllowAnonymous]
    [EnableRateLimiting("reads")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ToolUsageReadDTO>> Get(CancellationToken ct)
    {
        var result = await _toolUsageService.Get(ct);
        return result.TryPickT0(out var count, out var error) ?
            Ok(new ToolUsageReadDTO(count)) : this.GetActionResult(error);
    }

    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting("writes")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<ActionResult<ToolUsageReadDTO>> RegisterUse(CancellationToken ct)
    {
        var result = await _toolUsageService.RegisterUse(ct);
        return result.TryPickT0(out var count, out var error) ?
            Ok(new ToolUsageReadDTO(count)) : this.GetActionResult(error);
    }
}
