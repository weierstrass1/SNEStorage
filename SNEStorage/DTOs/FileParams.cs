using SNEStorage.Models;

namespace SNEStorage.DTOs;
public class FileParams
{
    public string DestinationPath { get; set; }
    public IFormFile File { get; set; }
}
