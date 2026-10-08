using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Shared;

public partial class ErrorPanel
{
    private readonly string _headingId = $"error-{Guid.NewGuid():N}";
    private ErrorCopy _copy = ApiErrorPresentation.Describe(500);
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    protected string LoginUrl => LoginNavigation.GetLoginUrl(Navigation);

    [Parameter] public int StatusCode { get; set; } = 500;
    [Parameter] public string? ErrorBody { get; set; }
    [Parameter] public string? Domain { get; set; }
    [Parameter] public EventCallback OnRetry { get; set; }

    protected override void OnParametersSet() => _copy = ApiErrorPresentation.Describe(StatusCode, ErrorBody, Domain);
}
