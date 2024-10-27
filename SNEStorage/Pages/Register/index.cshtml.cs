using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SNEStorage.DTOs;
using SNEStorage.Services;

namespace SNEStorage.Pages.Register
{
    public class RegisterModel : PageModel
    {
        [BindProperty]
        public RegisterInfo? DTO { get; set; }
        public AccountService AccountService { get; }

        public RegisterModel(AccountService accountService)
        {
            AccountService = accountService;
        }
        public async Task<IActionResult> OnPostAsync()
        {
            var res = await AccountService.Register(DTO!);
            string page = res == null ?
                "error" :
                "login";
            return RedirectToPage(page);
        }
    }
}
