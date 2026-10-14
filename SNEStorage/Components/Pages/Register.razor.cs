using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Pages;

public partial class Register : IAsyncDisposable
{
    private const string FormId = "register-form";
    private IJSObjectReference? _module;
    private readonly CancellationTokenSource _lifetime = new();
    private bool _saving;
    private bool _disposed;
    private string? _submissionError;
    private string[] _validationErrors = [];
    private bool _focusError;
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private IOptions<IdentityOptions> AccountOptions { get; set; } = default!;
    private const long DefaultTimeZoneId = 18;
    private const long DefaultEmailVisibilityId = 3;
    private List<IdNamePair> _timeZones = [];
    private List<IdNamePair> _emailVisibilities = [];
    private string? _optionsError;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    protected string LoginUrl => LoginNavigation.GetLoginUrl(Navigation);

    [Inject]
    private HttpClient Http { get; set; } = default!;

    [Parameter, SupplyParameterFromQuery(Name = "result")]
    public string? Result { get; set; }

    protected string InitialBirthday => DateTime.Today.ToString("yyyy-MM-dd");

    protected string? StatusMessage => _submissionError ?? (Result == "failed"
        ? "Registration failed. Check your details and try again." : null);

    protected async Task SubmitAsync()
    {
        if (_saving || _disposed) return;
        _saving = true;
        _submissionError = null;
        _validationErrors = [];
        try
        {
            await InitializeFormAsync();
            if (_disposed) return;
            var response = await _module!.InvokeAsync<RegistrationResponse>("submitRegistration", _lifetime.Token, FormId);
            if (_disposed) return;
            if (response.ValidationFailed) return;
            if (response.Status is >= 200 and < 300 && !string.IsNullOrWhiteSpace(response.RedirectUrl))
            {
                Navigation.NavigateTo(response.RedirectUrl, forceLoad: true);
                return;
            }
            _validationErrors = ApiErrorPresentation.ValidationErrors(response.Body);
            _submissionError = response.Cancelled || response.Status == 0 || response.Status is >= 200 and < 300
                ? "We could not confirm your registration. Check your email before submitting again; your information has been kept."
                : _validationErrors.Length > 0
                    ? "Check the following registration details. Your information has been kept."
                    : response.Body?.Contains("RegistrationFailed", StringComparison.Ordinal) == true
                        ? "Registration failed. Check your details and try again. Your information has been kept."
                        : ApiErrorPresentation.Describe(response.Status, response.Body, "account-upload").Message;
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException)
        {
            if (!_disposed) _submissionError = "We could not confirm your registration. Check your email before submitting again; your information has been kept.";
        }
        finally { _saving = false; _focusError = !_disposed && _submissionError is not null; }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed) return;
        try
        {
            if (firstRender) await InitializeFormAsync();
            if (!_focusError || _module is null) return;
            _focusError = false;
            await _module.InvokeVoidAsync("focusRegistrationError", _lifetime.Token);
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
    }

    private async Task InitializeFormAsync()
    {
        _module ??= await JavaScript.InvokeAsync<IJSObjectReference>("import", _lifetime.Token, "./js/register.js?v=10");
        if (_disposed) return;
        await _module.InvokeVoidAsync("initializeRegistration", _lifetime.Token, FormId, new
        {
            allowedUsernameCharacters = AccountOptions.Value.User.AllowedUserNameCharacters,
            password = AccountOptions.Value.Password
        });
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _lifetime.Cancel();
        if (_module is not null)
        {
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _module.InvokeVoidAsync("cancelRegistration", cleanup.Token, FormId);
                await _module.DisposeAsync();
            }
            catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
        }
        _lifetime.Dispose();
    }

    private sealed class RegistrationResponse
    {
        public int Status { get; set; }
        public string? Body { get; set; }
        public string? RedirectUrl { get; set; }
        public bool Cancelled { get; set; }
        public bool ValidationFailed { get; set; }
    }

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
