namespace SNEStorage.DTOs;

public class AuthenticationResponse
{
    public required string Token { get; set; }
    public DateTime ExpireTime { get; set; }
}
