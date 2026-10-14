using System.ComponentModel.DataAnnotations;

namespace SNEStorage.DTOs;

public class ResourceVersionEditDTO
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(25)]
    [RegularExpression(@"(?i)\A[0-9]{1,4}(?:\.[0-9]{1,4}){0,2}(?:-(?:alpha|beta|rc)(?:\.[0-9]{1,4})?)?\z")]
    public string? Version { get; set; }
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    [StringLength(10000, MinimumLength = 10)]
    public string? ReleaseNotes { get; set; }
    public bool? SealOfQualityRequested { get; set; }
}
