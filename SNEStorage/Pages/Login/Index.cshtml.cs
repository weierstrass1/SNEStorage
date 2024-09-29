using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Newtonsoft.Json;
using SNEStorage.DTOs;
using SNEStorage.Services;
using System.Text;

namespace SNEStorage.Pages.Login
{
    public class LoginModel : PageModel
    {
        static HttpClient httpClient = new();
        [BindProperty]
        public LoginCredentials? DTO { get; set; }
        public APIConfig ApiConfig { get; }

        public LoginModel(APIConfig apiConfig)
        {
            ApiConfig = apiConfig;
        }
        public async Task<IActionResult> OnPostAsync()
        {
            string payload = JsonConvert.SerializeObject(DTO);
            string path = ApiConfig.LoginAPIURL;

            HttpRequestMessage httpRequestMessage = new()
            {
                Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json")
            };

            try
            {
                HttpResponseMessage responseMessage = await httpClient.PostAsync(path, httpRequestMessage.Content);
                HttpContent content = responseMessage.Content;
                string message = await content.ReadAsStringAsync();
                AuthenticationResponse auth = JsonConvert.DeserializeObject<AuthenticationResponse>(message)!;
                HttpContext.Session.SetString("Token", auth.Token);
                return RedirectToPage("home");
            }
            catch (HttpRequestException exception)
            {
                Console.WriteLine("An HTTP request exception occurred. {0}", exception.Message);
                return RedirectToPage("error");
            }
        }
    }
}
