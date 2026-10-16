using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Shared;

public partial class ReportMenu
{
    [Parameter] public string Context { get; set; } = "resource";
    [Parameter] public long TargetId { get; set; }
    [Parameter] public string? UserId { get; set; }
    [Parameter] public bool? HasPreviews { get; set; }
    [Parameter] public bool? HasVersions { get; set; }
    private string PreviewsValue => BooleanValue(HasPreviews);
    private string VersionsValue => BooleanValue(HasVersions);
    private static string BooleanValue(bool? value) => value is null ? "unknown" : value.Value ? "true" : "false";
}
