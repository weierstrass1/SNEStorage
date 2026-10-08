using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Resources;

public partial class ResourceQualityRequest
{
    [Parameter] public bool Value { get; set; }
    [Parameter] public EventCallback<bool> ValueChanged { get; set; }
    private readonly string DescriptionId = $"quality-description-{Guid.NewGuid():N}";
    [Parameter] public string Description { get; set; } = "This seal certifies that SNEStorage staff have reviewed the resource and confirmed that it meets SNEStorage's quality standards.";
}
