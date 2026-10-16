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
[Route("api/news")]
public class NewsController(NewsService newsService) : Controller
{
    private readonly NewsService _newsService = newsService;

    [EnableRateLimiting("reads")]
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<List<NewsReadDTO>>> FullGet(
        [FromQuery] NewsFilterDTO filter,
        [FromQuery] NewsSorting sort = NewsSorting.RecentFirst,
        [FromQuery] int maxAmount = 10, [FromQuery] int page = 0,
        CancellationToken ct = default)
    {
        var result = await _newsService.FullGet(filter, sort, maxAmount, page, ct);
        return this.GetActionResult(result);
    }

    [EnableRateLimiting("writes")]
    [HttpPost("create")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<long>> Create(
        [FromBody] NewsCreateDTO dto, CancellationToken ct = default)
    {
        var result = await _newsService.Create(dto, ct);

        return result.TryPickT0(out var news, out var error) ?
            Ok(news.Id) : this.GetActionResult(error);
    }

    [EnableRateLimiting("writes")]
    [HttpPut("{newsId:long}/edit")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<long>> Edit(
        [FromRoute] long newsId, [FromBody] NewsEditDTO dto,
        CancellationToken ct = default)
    {
        var result = await _newsService.Edit(newsId, dto, ct);

        return result.TryPickT0(out var news, out var error) ?
            Ok(news.Id) : this.GetActionResult(error);
    }

    [EnableRateLimiting("reads")]
    [HttpGet("{newsId:long}")]
    [AllowAnonymous]
    public async Task<ActionResult<NewsReadDTO>> Get(
        [FromRoute] long newsId, CancellationToken ct = default)
    {
        return this.GetActionResult(await _newsService.Get(newsId, ct));
    }
    [EnableRateLimiting("votes")]
    [HttpPost("{newsId}/like")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("news_AddEditOrDeleteLike", CaptchaPolicy.After10TriesInShortTime)]
    public async Task<ActionResult<LikeUsers>> AddEditOrDeleteLike([FromRoute] long newsId,
    [FromForm] LikeValues value, CancellationToken ct = default)
    {
        var res = await _newsService.AddEditOrDeleteLike(newsId, value, ct);
        return this.GetActionResult(res);
    }
}
