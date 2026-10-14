using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNEStorage.DTOs;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/login")]
public class LoginController(AccountService accountService) : Controller
{
    private readonly AccountService _accountService = accountService;

    [EnableRateLimiting("login")]
    [HttpPost("submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromForm] LoginCredentials credentials, [FromForm] string? returnUrl = null)
    {
        var destination = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
        var result = await _accountService.Login(credentials);
        if (!result.TryPickT0(out var response, out _))
            return Redirect("/Login?result=invalid&returnUrl=" +
                Uri.EscapeDataString(destination));

        HttpContext.Response.Cookies.Append("__Host-SNEStorage.Auth", response.Token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = new DateTimeOffset(response.ExpireTime)
            });
        return LocalRedirect(destination);
    }
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Logout()
    {
        var result = await _accountService.Logout();
        return result.Result == ServiceResult.Success ?
            Redirect("/") : Redirect("/?result=logout_failed");
    }
}
