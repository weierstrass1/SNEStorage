namespace SNEStorage.DTOs;

public class APIConfigDTO
{
    public required string Host {  get; set; }
    public required string Account { get; set; }
    public required string Login { get; set; }
    public required string Register { get; set; }
    public required string Email { get; set; }
    public required int EmailPort { get; set; }
    public required string EmailSMPTP { get; set; }
    public required string EmailPassword { get; set; }
    public required string TurnstileSiteKey { get; set; }
    public required string[] TurnstileAllowedHostnames { get; set; }
}
