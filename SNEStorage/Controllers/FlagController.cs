using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SNEStorage.DTOs;
using SNEStorage.Filters.Attributes;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/flags")]
public class FlagController(FlagsService flagsService,
    CacheService cacheService) : Controller
{
    private readonly FlagsService _flagsService = flagsService;
    private readonly CacheService _cacheService = cacheService;
    [EnableRateLimiting("reads")]
    [HttpGet("options")]
    public async Task<ActionResult<Dictionary<long, Reason>>> GetCreateOptions(CancellationToken ct)
    {
        var res = await _cacheService.GetAllReasons(ct);
        return this.GetActionResult(res);
    }
    [EnableRateLimiting("reads")]
    [HttpGet]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<List<FlagReadDTO>>> FullGet([FromQuery] FlagFilterDTO filter,
    [FromQuery] FlagSort sort = FlagSort.HighestPriority, [FromQuery] int maxAmount = 10,
    [FromQuery] int page = 0, CancellationToken ct = default)
    {
        var result = await _flagsService.FullGet(filter, sort, maxAmount, page, ct);
        return this.GetActionResult(result);
    }
    [EnableRateLimiting("writes")]
    [HttpPost("resource_flag/create")]
    [RequireCaptcha("create_flag", policy: CaptchaPolicy.Always)]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<long>> CreateResourceFlag([FromBody] ResourceFlagCreateDTO dto, 
        CancellationToken ct = default)
    {
        var result = await _flagsService.Create(dto, ct);

        return result.TryPickT0(out var flag, out var error) ?
            Ok(flag.Id) : this.GetActionResult(error);
    }
    [EnableRateLimiting("writes")]
    [HttpPost("comment_flag/create")]
    [RequireCaptcha("create_flag", policy: CaptchaPolicy.Always)]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<long>> CreateResourceFlag([FromBody] CommentFlagCreateDTO dto,
    CancellationToken ct = default)
    {
        var result = await _flagsService.Create(dto, ct);

        return result.TryPickT0(out var flag, out var error) ?
            Ok(flag.Id) : this.GetActionResult(error);
    }
    [EnableRateLimiting("writes")]
    [HttpPut("{flagId:long}/edit")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<long>> Edit([FromRoute] long flagId, [FromBody] bool resolved,
        CancellationToken ct = default)
    {
        var result = await _flagsService.Edit(flagId, resolved, ct);

        return result.TryPickT0(out var news, out var error) ?
            Ok(news.Id) : this.GetActionResult(error);
    }
}
