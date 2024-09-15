using Microsoft.AspNetCore.Mvc;
using SNEStorage.DTOs;

namespace SNEStorage.Controllers;

public class LoginController : Controller
{
    public AccountController AccountController { get; }
    public LoginController(AccountController accountController)
    {
        AccountController = accountController;
    }
    public IActionResult Index()
    {
        return View();
    }
    [HttpPost("login")]
    public async Task<ActionResult<bool>> Login(LoginCredentials credentials)
    {
        var result = await AccountController.Login(credentials);
        if (result.Value == null)
            return Redirect("loginfailed");
        return Redirect("Home");
    }
    public IActionResult LoginFailed()
    {
        return View();
    }
}
