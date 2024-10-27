using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SNEStorage.DTOs;
using SNEStorage.Services;

namespace SNEStorage.Pages.Login
{
    public class LoginModel : PageModel
    {
        [BindProperty]
        public LoginCredentials? DTO { get; set; }
        public AccountService AccountService { get; }
        public LoginModel(AccountService accountService)
        {
            AccountService = accountService;
        }
        public async Task<IActionResult> OnPostAsync()
        {
            var res = await AccountService.Login(DTO!);
            string page = res == null ?
                "error" :
                "home";
            return RedirectToPage(page);
        }
    }
}
