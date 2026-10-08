using Microsoft.AspNetCore.Mvc;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/externa-author")]
public class ExternalAuthorController(ExternalAuthorService externalAuthorService) : Controller
{
    private readonly ExternalAuthorService _externalAuthorService = externalAuthorService;
    [HttpGet]
    public async Task<ActionResult<List<string>>> GetExternalAuthorsLike([FromQuery] string likeString, [FromQuery] int maxAmount = 10, CancellationToken ct = default)
    {
        var externalAuthors = await _externalAuthorService.GetAuthorLike(likeString, maxAmount, ct);
        return Ok(externalAuthors);
    }
}
