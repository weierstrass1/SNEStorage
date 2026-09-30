using Microsoft.AspNetCore.Mvc;
using SNEStorage.DTOs;

namespace SNEStorage.Controllers;

public class LoginController : Controller
{
    public IActionResult Index()
    {
        return View();
    }
}
