using Microsoft.AspNetCore.Mvc;
using SNEStorage.DTOs;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/user")]
public class UserController(UserService userService,
    DBHelperService dbHelperService) : Controller
{
    private readonly UserService _userService = userService;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    [HttpGet("search")]
    public async Task<ActionResult<List<UserNameDTO>>> GetUsersLike([FromQuery] string likeString, [FromQuery] int maxAmount = 10, CancellationToken ct = default)
    {
        var users = await _dbHelperService.TryFunction(
            async cancel => await _userService.GetUserLike(likeString, maxAmount, cancel),
            ct: ct);
        return users != null ?
            Ok(users) :
            BadRequest();
    }
}
