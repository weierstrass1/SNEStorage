using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Controllers;

[Route("auth")]
public sealed class WebAuthController(SignInManager<ApplicationUser> signInManager) : Controller
{
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([FromForm] LoginCredentials credentials, [FromForm] string? returnUrl)
    {
        if (!ModelState.IsValid)
            return LocalRedirect("/Login?error=invalid");

        var result = await signInManager.PasswordSignInAsync(
            credentials.User!,
            credentials.Password!,
            isPersistent: false,
            lockoutOnFailure: false);

        if (!result.Succeeded)
            return LocalRedirect("/Login?error=credentials");

        return Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl!)
            : LocalRedirect("/");
    }

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await signInManager.SignOutAsync();
        return LocalRedirect("/");
    }
}
