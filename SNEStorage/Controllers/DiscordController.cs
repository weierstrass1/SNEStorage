using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using OneOf;
using Microsoft.Extensions.Options;
using SNEStorage.Discord;
using SNEStorage.Services;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/discord")]
public class DiscordController(DiscordService discordService,
    AccountService accountService,
    IOptions<DiscordDTO> discordOptions) : Controller
{
    private const string OAuthStateSessionKey = "DiscordOAuthState";
    private const string LinkStartPath = "/api/discord/link/start";
    private readonly DiscordService _discordService = discordService;
    private readonly AccountService _accountService = accountService;
    private readonly DiscordDTO _discordOptions = discordOptions.Value;
    [NonAction]
    public ActionResult<T> GetActionResult<T>(OneOf<T,DiscordServiceResult> result)
    {
        return result.TryPickT0(out var item, out var error) ?
            Ok(item) : GetActionResult(error);
    }
    [HttpGet("link/start")]
    [AllowAnonymous]
    public IActionResult StartLink()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Redirect("/Login?returnUrl=" +
                Uri.EscapeDataString(LinkStartPath));
        }

        string state = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));

        HttpContext.Session.SetString(OAuthStateSessionKey,state);

        string authorizationUrl = _discordService.CreateAuthorizationUrl(state);

        return Redirect(authorizationUrl);
    }
    [HttpGet("link/callback")]
    [Authorize(AuthenticationSchemes =
    JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> LinkCallback([FromQuery] string? code,
        [FromQuery] string? state, [FromQuery] string? error, CancellationToken ct)
    {
        if (!ConsumeOAuthState(state))
            return BadRequest("DiscordController-InvalidOAuthState");

        if (!string.IsNullOrWhiteSpace(error))
            return Redirect("/Profile/Edit?discord=authorization-denied");
        
        string? userId = _accountService.GetUserID();

        var result = await _discordService.CompleteAuthorizationAsync(userId, code, ct);

        return result == DiscordServiceResult.Success ?
            Redirect(_discordOptions.InviteUrl) :
            GetActionResult(result);
    }
    [NonAction]
    public ObjectResult GetActionResult(DiscordServiceResult result)
    {
        string res = $"{GetType().Name}-{result}";

        return result switch
        {
            DiscordServiceResult.Success => Ok(res),

            DiscordServiceResult.InvalidUserId or
            DiscordServiceResult.InvalidDiscordUserId or
            DiscordServiceResult.InvalidDiscordUsername or
            DiscordServiceResult.InvalidOAuthCode => BadRequest(res),

            DiscordServiceResult.OAuthTokenExchangeError or
            DiscordServiceResult.OAuthUserLookupError =>
                StatusCode(StatusCodes.Status502BadGateway, res),

            DiscordServiceResult.UserNotFound or
            DiscordServiceResult.DiscordAccountNotLinked => NotFound(res),

            DiscordServiceResult.EmailNotConfirmed or
            DiscordServiceResult.BannedUser =>
                StatusCode(StatusCodes.Status403Forbidden, res),

            DiscordServiceResult.DiscordAccountLinkedToAnotherUser =>
                Conflict(res),

            DiscordServiceResult.DBError =>
                StatusCode(StatusCodes.Status500InternalServerError, res),

            _ => StatusCode(StatusCodes.Status500InternalServerError, res)
        };
    }
    private bool ConsumeOAuthState(string? receivedState)
    {
        string? expectedState = HttpContext.Session.GetString(OAuthStateSessionKey);

        HttpContext.Session.Remove(OAuthStateSessionKey);

        if (string.IsNullOrWhiteSpace(expectedState) ||
            string.IsNullOrWhiteSpace(receivedState))
            return false;

        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedState);

        byte[] receivedBytes = Encoding.UTF8.GetBytes(receivedState);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, receivedBytes);
    }
}
