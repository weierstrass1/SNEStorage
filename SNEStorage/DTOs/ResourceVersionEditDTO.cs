using System.ComponentModel.DataAnnotations;

namespace SNEStorage.DTOs;
public class ResourceVersionEditDTO
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Version { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? ReleaseNotes { get; set; }
    public bool? SealOfQualityRequested { get; set; }
}
