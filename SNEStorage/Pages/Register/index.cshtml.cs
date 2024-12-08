using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorage.Pages.Register
{
    [IgnoreAntiforgeryToken(Order = 1001)]
    public class RegisterModel : PageModel
    {
        [BindProperty]
        public RegisterInfo? DTO { get; set; }
        public AccountService AccountService { get; }
        public SnestorageContext Context { get; }
        public SelectList? TimeZones {  get; set; }
        public SelectList? EmailVisibilities { get; set; }

        public RegisterModel(AccountService accountService,
            SnestorageContext context)
        {
            AccountService = accountService;
            Context = context;
        }
        public async Task<IActionResult> OnGetAsync()
        {
            DTO = new();
            TimeZones = new SelectList(Context.TimeZones, "Id", "Name", 18);
            EmailVisibilities = new SelectList(Context.Visibilities.Where(v => !v.ResourceOnly), "Id", "Name", 3);
            DTO.TimeZoneId = (int)TimeZones.SelectedValue;
            DTO.EmailVisibilityId = (int)EmailVisibilities.SelectedValue;
            DTO.UsernameColor = "#FFFFFF";
            DTO.Birthday = DateOnly.FromDateTime(DateTime.Now);
            return Page();
        }
        public async Task<IActionResult> OnPostAsync()
        {
            var res = await AccountService.Register(DTO!);
            if (res?.Value == null)
                return Redirect("registerfailed");
            return Redirect("registersuccess");
        }
    }
}
