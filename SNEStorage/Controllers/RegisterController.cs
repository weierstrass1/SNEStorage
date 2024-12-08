using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SNEStorage.Models;

namespace SNEStorage.Controllers
{
    public class RegisterController : Controller
    {
        public AccountController AccountController { get; }
        public SnestorageContext Context { get; }

        public RegisterController(AccountController accountController,
            SnestorageContext context)
        {
            AccountController = accountController;
            Context = context;
        }
        public IActionResult Index()
        {
            return View();
        }
        public IActionResult RegisterSuccess()
        {
            return View();
        }
        public IActionResult RegisterFailed()
        {
            return View();
        }
    }
}
