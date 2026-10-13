using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Resources;

public partial class ResourceQualityRequest
{
    [Parameter] public bool Value { get; set; }
    [Parameter] public EventCallback<bool> ValueChanged { get; set; }
    private readonly string DescriptionId = $"quality-description-{Guid.NewGuid():N}";
    [Parameter] public string Description { get; set; } = "The seal certifies that a specific version has been reviewed by SNEStorage staff and meets our minimum quality standards.";
}
