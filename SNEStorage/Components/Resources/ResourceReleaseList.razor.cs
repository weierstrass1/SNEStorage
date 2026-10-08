using System.Globalization;
using Microsoft.AspNetCore.Components;
using SNEStorage.Models;

namespace SNEStorage.Components.Resources;

public partial class ResourceReleaseList
{
    protected static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-US");
    [Parameter] public long ResourceId { get; set; }
    [Parameter] public IReadOnlyList<ResourceVersion> Versions { get; set; } = [];
    [Parameter] public bool CanManage { get; set; }
    protected IReadOnlyList<ResourceVersion> OrderedVersions { get; private set; } = [];
    protected override void OnParametersSet() => OrderedVersions = Versions.OrderByDescending(version => version.CreatedAt).ThenByDescending(version => version.Id).ToArray();
    protected static string VersionName(ResourceVersion version) => string.IsNullOrWhiteSpace(version.Version) ? "Release" : version.Version;
    protected string? DownloadUrl(ResourceVersion version) => ResourceFileLinks.For(ResourceId, version.FileId > 0 ? version.FileId : version.File?.Id ?? 0, true);
}
