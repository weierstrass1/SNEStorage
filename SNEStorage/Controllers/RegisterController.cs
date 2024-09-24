using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SNEStorage.DTOs;
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
            ViewData["Title"] = "Register";
            ViewData["TimeZones"] = new SelectList(Context.TimeZones, "Id", "Name", 18);
            ViewData["Visibilities"] = new SelectList(Context.Visibilities.Where(v => !v.ResourceOnly), "Id", "Name", 3);
            return View();
        }
        [HttpPost("register")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult<bool>> Register(RegisterInfo regInfo)
        {
            var result = await AccountController.Register(regInfo);
            if (result.Value == null)
                return Redirect("registerfailed");
            return Redirect("registersuccess");
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
