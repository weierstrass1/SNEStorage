using SNEStorage.DTOs;

namespace SNEStorage.Services
{
    public class APIConfig
    {
        public IConfiguration Configuration { get; }
        public APIConfigDTO DTO { get; set; }
        public APIConfig(IConfiguration configuration)
        {
            Configuration = configuration;
            DTO = configuration.GetSection("APISettings").Get<APIConfigDTO>()!;
        }
        public string APIURL => $"{DTO.Host}";
        public string AccountAPIURL => $"{DTO.Host}/{DTO.Account}";
    }
}
