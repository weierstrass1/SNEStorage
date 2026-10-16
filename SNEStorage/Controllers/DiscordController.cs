using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;
using SNEStorage.Services;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using SNEStorage.DTOs;

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
    public async Task<IActionResult> LinkCallback([FromQuery, StringLength(TextLimits.OAUTH_CODE)] string? code,
        [FromQuery, StringLength(TextLimits.OAUTH_STATE)] string? state,
        [FromQuery, StringLength(TextLimits.SEARCH)] string? error, CancellationToken ct)
    {
        if (!ConsumeOAuthState(state))
            return BadRequest("DiscordController-InvalidOAuthState");

        if (!string.IsNullOrWhiteSpace(error))
            return Redirect("/Profile/Edit?discord=authorization-denied");
        
        string? userId = _accountService.GetUserID();

        var result = await _discordService.CompleteAuthorizationAsync(userId, code, ct);

        return result.Result == ServiceResult.Success ?
            Redirect(_discordOptions.InviteUrl) :
            this.GetActionResult(result);
    }
    private bool ConsumeOAuthState(string? receivedState)
    {
        string? expectedState = HttpContext.Session.GetString(OAuthStateSessionKey);

        HttpContext.Session.Remove(OAuthStateSessionKey);

        if (string.IsNullOrWhiteSpace(expectedState) ||
            string.IsNullOrWhiteSpace(receivedState) || receivedState.Length != TextLimits.OAUTH_STATE)
            return false;

        byte[] expectedBytes = Encoding.UTF8.GetBytes(expectedState);

        byte[] receivedBytes = Encoding.UTF8.GetBytes(receivedState);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, receivedBytes);
    }
}
