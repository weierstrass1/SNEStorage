using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Pages;

public partial class Error
{
    [Parameter]
    public int? StatusCode { get; set; }

    [SupplyParameterFromQuery(Name = "error")]
    public string? ErrorCode { get; set; }

    [SupplyParameterFromQuery(Name = "source")]
    public string? ErrorSource { get; set; }

    protected int EffectiveStatusCode => StatusCode is >= 400 and <= 599 ? StatusCode.Value : 500;
    protected string ErrorTitle => ApiErrorPresentation.Describe(EffectiveStatusCode, ErrorCode, ErrorSource).Title;
}
