namespace SNEStorage.DTOs;
public class RegisterResponseDTO
{
    public required string UserId { get; set; }
    public bool RequiresEmailConfirmation { get; set; } = true;
    public bool ConfirmationEmailSent { get; set; }
}
