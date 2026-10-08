using Microsoft.AspNetCore.Mvc;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/videogames")]
public class VideogameController(VideogameService videogameService) : Controller
{
    private readonly VideogameService _videogameService = videogameService;
    [HttpGet("search")]
    public async Task<ActionResult<List<Videogame>>> GetVideogamesLike([FromQuery] string? likeString = null, [FromQuery] int maxAmount = 10, CancellationToken ct = default)
    {
        var videogames = await _videogameService.GetVideogamesLike(likeString?? string.Empty, maxAmount, ct);
        return Ok(videogames);
    }
}
