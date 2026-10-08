using Microsoft.AspNetCore.Components;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Pages;

public partial class Register
{
    private const long DefaultTimeZoneId = 18;
    private const long DefaultEmailVisibilityId = 3;
    private List<SelectOptionDto> _timeZones = [];
    private List<SelectOptionDto> _emailVisibilities = [];
    private string? _optionsError;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    protected string LoginUrl => LoginNavigation.GetLoginUrl(Navigation);

    [Inject]
    private HttpClient Http { get; set; } = default!;

    [Parameter, SupplyParameterFromQuery(Name = "result")]
    public string? Result { get; set; }

    protected string InitialBirthday => DateTime.Today.ToString("yyyy-MM-dd");

    protected string? StatusMessage => Result == "failed"
        ? "Registration failed. Check your details and try again."
        : null;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var options = await Http.GetFromJsonAsync<RegistrationOptionsDto>("register/options");
            _timeZones = options?.TimeZones.ToList() ?? [];
            _emailVisibilities = options?.EmailVisibilities.ToList() ?? [];
        }
        catch (HttpRequestException)
        {
            _optionsError = "Registration options could not be loaded. Please try again later.";
        }
    }
}
