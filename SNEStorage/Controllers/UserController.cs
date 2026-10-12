using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using SNEStorage.DTOs;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/user")]
public class UserController(UserService userService,
    GetLikeController getLikeController) : Controller
{
    private readonly UserService _userService = userService;
    private readonly GetLikeController _getLikeController = getLikeController;
    [EnableRateLimiting("reads")]
    [HttpGet("search")]
    public async Task<ActionResult<List<UserNameDTO>>> GetUsersLike([FromQuery] string likeString, [FromQuery] int maxAmount = 10, CancellationToken ct = default)
    {
        var users = await _userService.GetUserLike(likeString, maxAmount, ct);
        return _getLikeController.GetActionResult(users);
    }
}
