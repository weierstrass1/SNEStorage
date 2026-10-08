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

        HttpContext.Session.SetString("Token", response.Value.Token);
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
