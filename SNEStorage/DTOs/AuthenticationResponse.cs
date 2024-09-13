namespace SNEStorage.DTOs
{
    public class AuthenticationResponse
    {
        public string Token { get; set; }
        public DateTime ExpireTime { get; set; }
    }
}
