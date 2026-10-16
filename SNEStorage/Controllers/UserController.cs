using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using SNEStorage.DTOs;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/user")]
public class UserController(UserService userService) : Controller
{
    private readonly UserService _userService = userService;

    [EnableRateLimiting("reads")]
    [HttpGet("search")]
    public async Task<ActionResult<List<UserNameDTO>>> GetUsersLike([FromQuery] string? likeString = null,
        [FromQuery] int maxAmount = 10, [FromQuery] int page = 0, [FromQuery] bool withAvatar = false,
        CancellationToken ct = default)
    {
        var users = await _userService.GetUserLike(likeString ?? string.Empty, maxAmount, 
            page, withAvatar, ct);
        return this.GetActionResult(users);
    }
}
