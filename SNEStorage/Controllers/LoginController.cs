using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNEStorage.DTOs;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/login")]
public class LoginController(AccountController accountController) : Controller
{
    private readonly AccountController _accountController = accountController;

    [EnableRateLimiting("login")]
    [HttpPost("submit")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromForm] LoginCredentials credentials, [FromForm] string? returnUrl = null)
    {
        var destination = Url.IsLocalUrl(returnUrl) ? returnUrl! : "/";
        var response = await _accountController.Login(credentials);
        if (response.Value is null)
        {
            return Redirect("/Login?result=invalid&returnUrl=" +
                Uri.EscapeDataString(destination));
        }

        HttpContext.Response.Cookies.Append("__Host-SNEStorage.Auth", response.Value.Token,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Path = "/",
                Expires = new DateTimeOffset(response.Value.ExpireTime)
            });
        return LocalRedirect(destination);
    }
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<IActionResult> Logout()
    {
        return await _accountController.Logout() is OkResult
            ? Redirect("/")
            : Redirect("/?result=logout_failed");
    }
}
