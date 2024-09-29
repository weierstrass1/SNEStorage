using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using SNEStorage.DTOs;
using SNEStorage.Services;

namespace SNEStorage.Pages.Register
{
    public class RegisterModel : PageModel
    {
        static HttpClient httpClient = new();
        [BindProperty]
        public RegisterInfo? DTO { get; set; }
        public APIConfig ApiConfig { get; }

        public RegisterModel(APIConfig apiConfig)
        {
            ApiConfig = apiConfig;
        }
    }
}
