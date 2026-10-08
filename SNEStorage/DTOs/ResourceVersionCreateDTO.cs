namespace SNEStorage.DTOs;
public class ResourceVersionCreateDTO
{
    public required string Version { get; set; }
    public required string ReleaseNotes { get; set; }
    public bool SealOfQualityRequested { get; set; } = false;
    public required IFormFile File { get; set; }
}
