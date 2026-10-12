using SNEStorage.DTOs;

namespace SNEStorage.Services;

public class APIConfig(IConfiguration configuration)
{
    private readonly IConfiguration _configuration = configuration;
    private readonly APIConfigDTO _dto = configuration.GetSection("APISettings").Get<APIConfigDTO>()!;

    public string APIURL => $"{_dto.Host}";
    public string AccountAPIURL => $"{_dto.Host}/{_dto.Account}";
    public string LoginAPIURL => $"{_dto.Host}/{_dto.Account}/{_dto.Login}";
    public string RegisterAPIURL => $"{_dto.Host}/{_dto.Account}/{_dto.Register}";
    public string Email => _dto.Email;
    public int EmailPort => _dto.EmailPort;
    public string EmailSMPTP => _dto.EmailSMPTP;
    public string EmailPassword => _dto.EmailPassword;
    public string TurnstileSiteKey => _dto.TurnstileSiteKey;
    public string TurnstileSecretKey => _configuration["Turnstile:SecretKey"]!;
    public string[] TurnstileAllowedHostnames => _dto.TurnstileAllowedHostnames;
}
