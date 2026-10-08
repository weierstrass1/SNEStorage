using SNEStorage.DTOs;

namespace SNEStorage.Services;

public class APIConfig(IConfiguration configuration)
{
    public IConfiguration Configuration { get; } = configuration;
    public APIConfigDTO DTO { get; set; } = configuration.GetSection("APISettings").Get<APIConfigDTO>()!;

    public string APIURL => $"{DTO.Host}";
    public string AccountAPIURL => $"{DTO.Host}/{DTO.Account}";
    public string LoginAPIURL => $"{DTO.Host}/{DTO.Account}/{DTO.Login}";
    public string RegisterAPIURL => $"{DTO.Host}/{DTO.Account}/{DTO.Register}";
    public string Email => DTO.Email;
    public string EmailPassword => DTO.EmailPassword;
}
