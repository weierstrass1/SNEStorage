using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneOf;
using SNEStorage.DTOs;
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
    public async Task<ActionResult<List<NewsReadDTO>>> FullGet([FromQuery] NewsFilterDTO filter,
        [FromQuery] NewsSorting sort = NewsSorting.RecentFirst, [FromQuery] int maxAmount = 10,
        [FromQuery] int page = 0, CancellationToken ct = default)
    {
        var res = await _newsService.FullGet(
            filter, sort, maxAmount, page, ct);

        return GetActionResult(res);
    }
    [EnableRateLimiting("writes")]
    [HttpPost("create")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<long>> Create([FromBody] NewsCreateDTO dto,
        CancellationToken ct = default)
    {
        var res = await _newsService.Create(dto, ct);

        return res.TryPickT0(out var news, out var error) ?
            Ok(news.Id) : GetActionResult(error);
    }
    [EnableRateLimiting("writes")]
    [HttpPut("{newsId:long}/edit")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<long>> Edit([FromRoute] long newsId, [FromBody] NewsEditDTO dto,
        CancellationToken ct = default)
    {
        var res = await _newsService.Edit(newsId, dto, ct);

        return res.TryPickT0(out var news, out var error) ?
            Ok(news.Id) : GetActionResult(error);
    }
    private ObjectResult GetActionResult(NewsServiceResult result)
    {
        string res = $"{GetType().Name}-{result}";
        bool authenticated =
            HttpContext?.User.Identity?.IsAuthenticated == true;

        return result switch
        {
            NewsServiceResult.Success => Ok(res),

            NewsServiceResult.Unauthorized when authenticated =>
                StatusCode(StatusCodes.Status403Forbidden, res),

            NewsServiceResult.Unauthorized =>
                Unauthorized(res),

            NewsServiceResult.UserNotFound or
            NewsServiceResult.NewNotFound =>
                NotFound(res),

            NewsServiceResult.InvalidTitle or
            NewsServiceResult.InvalidText or
            NewsServiceResult.InvalidSort or
            NewsServiceResult.InvalidMaxAmount or
            NewsServiceResult.InvalidPage or
            NewsServiceResult.SkipOutOfRange =>
                BadRequest(res),

            NewsServiceResult.DBError =>
                StatusCode(StatusCodes.Status500InternalServerError, res),

            _ => StatusCode(StatusCodes.Status500InternalServerError, res)
        };
    }
    private ActionResult<T> GetActionResult<T>(
        OneOf<T, NewsServiceResult> result)
    {
        return result.TryPickT0(out var entity, out var error) ?
            Ok(entity) : GetActionResult(error);
    }
}
