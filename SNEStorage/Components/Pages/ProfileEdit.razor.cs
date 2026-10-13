using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Components.Shared;

namespace SNEStorage.Components.Pages;

[RequireAccount]
public partial class ProfileEdit : IAsyncDisposable
{
    private const string AvatarInputId = "profile-avatar-file";
    private static readonly CultureInfo EditCulture = CultureInfo.GetCultureInfo("en-US");
    private readonly CancellationTokenSource _lifetime = new();

    private IJSObjectReference? _profileModule;
    private UserInfo? _profile;
    private ProfileEditForm? _form;
    private ProfileEditForm? _initialForm;
    private List<IdNamePair> _timeZones = [];
    private List<IdNamePair> _emailVisibilities = [];
    private bool _avatarChanged;
    private string? _error;
    private string? _feedback;
    private string? _optionsError;
    private bool _isAuthenticated;
    private bool _isLoading = true;
    private bool _isSaving;
    private bool _saveSucceeded;
    private bool _profileNotFound;
    private bool _disposed;
    protected string LoginUrl => LoginNavigation.GetLoginUrl(Navigation);

    [Parameter]
    [SupplyParameterFromQuery(Name = "discord")]
    public string? DiscordResult { get; set; }

    [CascadingParameter(Name = "VerifiedPageAccount")]
    private AccountPageContext? PageAccount { get; set; }

    [Inject]
    private HttpClient Http { get; set; } = default!;

    [Inject]
    private IJSRuntime JavaScript { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    protected void ReloadProfile() => Navigation.Refresh(forceReload: true);

    protected string ProfileName => string.IsNullOrWhiteSpace(_profile?.UserSummary?.Username)
        ? "Player" : _profile.UserSummary.Username;

    protected string DiscordDisplayName => string.IsNullOrWhiteSpace(_profile?.DiscordUsername)
        ? "Discord account verified"
        : $"@{_profile.DiscordUsername}";

    protected string AvatarPreviewUrl =>
        PublicFileUrl.Resolve(_profile?.UserSummary?.Avatar?.URL ?? _profile?.Avatar?.URL, "/images/caja.png");

    protected bool HasChanges =>
        _form is not null && _initialForm is not null &&
        (_avatarChanged || GetChangedFields().Count > 0);

    protected bool HasInvalidSocialLinks => GetSocialLinkErrors().Count > 0;

    protected string? SocialLinkError(string fieldName) =>
        GetSocialLinkErrors().TryGetValue(fieldName, out var error) ? error : null;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var options = await Http.GetFromJsonAsync<RegistrationOptionsDto>("register/options", _lifetime.Token);
            if (_disposed) return;
            _timeZones = options?.TimeZones.ToList() ?? [];
            _emailVisibilities = options?.EmailVisibilities.ToList() ?? [];
        }
        catch (OperationCanceledException)
        {
            if (!_disposed) _optionsError = "Some profile options could not be loaded.";
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException)
        {
            _optionsError = "Some profile options could not be loaded.";
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed) return;
        _profile = PageAccount?.Profile;
        _isAuthenticated = _profile is not null;
        _profileNotFound = _profile is null;
        if (_profile is not null)
        {
            _form = ProfileEditForm.From(_profile);
            _initialForm = _form.Copy();
            ApplyDiscordFeedback();
        }
        try
        {
            _profileModule = await JavaScript.InvokeAsync<IJSObjectReference>("import", _lifetime.Token, "./js/profile.js?v=10");
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException)
        {
            if (!_disposed) _error = "The profile editor could not be loaded. Please try again later.";
        }
        finally
        {
            if (!_disposed) { _isLoading = false; StateHasChanged(); }
        }
    }

    protected void AvatarChanged(bool selected)
    {
        _avatarChanged = selected;
        _feedback = null;
        _saveSucceeded = false;
    }

    protected async Task SaveChangesAsync()
    {
        if (!_isAuthenticated || _disposed || _isSaving || _profileModule is null || _form is null || _initialForm is null || !HasChanges)
        {
            return;
        }

        if (HasInvalidSocialLinks)
        {
            _feedback = "Fix the highlighted social fields before saving.";
            return;
        }

        _isSaving = true;
        _feedback = null;
        _saveSucceeded = false;

        try
        {
            var responseJson = await _profileModule.InvokeAsync<string>(
                "updateProfile",
                _lifetime.Token,
                "/api/profile/me/edit",
                GetChangedFields(),
                AvatarInputId);

            if (_disposed) return;

            using var response = JsonDocument.Parse(responseJson);
            var statusCode = response.RootElement.GetProperty("status").GetInt32();
            var body = response.RootElement.TryGetProperty("body", out var responseBody)
                && responseBody.ValueKind == JsonValueKind.String ? responseBody.GetString() : null;

            if (statusCode is >= 200 and < 300)
            {
                Navigation.NavigateTo("/Profile/me", forceLoad: true);
                return;
            }

            if (statusCode is 401 or 403) { _isAuthenticated = false; _profile = null; _form = null; }

            _feedback = ApiErrorPresentation.Describe(statusCode, body, "account-upload").Message;
        }
        catch (OperationCanceledException)
        {
            if (!_disposed) _feedback = "The save response was not received. Reload your profile to check your changes before trying again.";
        }
        catch (JSDisconnectedException) { }
        catch (JSException)
        {
            _feedback = "Your changes could not be sent. Please check your connection and try again.";
        }
        catch (JsonException)
        {
            _feedback = "The save response could not be read.";
        }
        finally
        {
            _isSaving = false;
            _saveSucceeded = false;
        }
    }

    private Dictionary<string, string> GetChangedFields()
    {
        var changed = new Dictionary<string, string>();
        if (_form is null || _initialForm is null)
        {
            return changed;
        }

        AddIfChanged(changed, nameof(EditProfileDTO.UsernameColor), _form.UsernameColor, _initialForm.UsernameColor);
        AddIfChanged(changed, nameof(EditProfileDTO.Bio), _form.Bio, _initialForm.Bio);
        AddSocialIfChanged(changed, nameof(EditProfileDTO.Patreon), "Patreon", _form.Patreon, _initialForm.Patreon);
        AddSocialIfChanged(changed, nameof(EditProfileDTO.Paypal), "PayPal", _form.Paypal, _initialForm.Paypal);
        AddSocialIfChanged(changed, nameof(EditProfileDTO.BuyMeACoffee), "Buy Me a Coffee", _form.BuyMeACoffee, _initialForm.BuyMeACoffee);
        AddSocialIfChanged(changed, nameof(EditProfileDTO.Twitter), "Twitter / X", _form.Twitter, _initialForm.Twitter);
        AddSocialIfChanged(changed, nameof(EditProfileDTO.Facebook), "Facebook", _form.Facebook, _initialForm.Facebook);
        AddSocialIfChanged(changed, nameof(EditProfileDTO.Youtube), "YouTube", _form.Youtube, _initialForm.Youtube);
        AddSocialIfChanged(changed, nameof(EditProfileDTO.Instagram), "Instagram", _form.Instagram, _initialForm.Instagram);
        AddSocialIfChanged(changed, nameof(EditProfileDTO.Steam), "Steam", _form.Steam, _initialForm.Steam);
        AddSocialIfChanged(changed, nameof(EditProfileDTO.Twitch), "Twitch", _form.Twitch, _initialForm.Twitch);
        AddSocialIfChanged(changed, nameof(EditProfileDTO.Github), "GitHub", _form.Github, _initialForm.Github);
        AddSocialIfChanged(changed, nameof(EditProfileDTO.NintendoNetwork), "Nintendo Network", _form.NintendoNetwork, _initialForm.NintendoNetwork);
        AddIfChanged(changed, nameof(EditProfileDTO.EmailVisibilityId), _form.EmailVisibilityId, _initialForm.EmailVisibilityId);
        AddIfChanged(changed, nameof(EditProfileDTO.TimeZoneId), _form.TimeZoneId, _initialForm.TimeZoneId);

        return changed;
    }

    private Dictionary<string, string> GetSocialLinkErrors()
    {
        var errors = new Dictionary<string, string>();
        if (_form is null || _initialForm is null)
        {
            return errors;
        }

        AddSocialError(errors, nameof(EditProfileDTO.Patreon), "Patreon", _form.Patreon, _initialForm.Patreon);
        AddSocialError(errors, nameof(EditProfileDTO.Paypal), "PayPal", _form.Paypal, _initialForm.Paypal);
        AddSocialError(errors, nameof(EditProfileDTO.BuyMeACoffee), "Buy Me a Coffee", _form.BuyMeACoffee, _initialForm.BuyMeACoffee);
        AddSocialError(errors, nameof(EditProfileDTO.Twitter), "Twitter / X", _form.Twitter, _initialForm.Twitter);
        AddSocialError(errors, nameof(EditProfileDTO.Facebook), "Facebook", _form.Facebook, _initialForm.Facebook);
        AddSocialError(errors, nameof(EditProfileDTO.Youtube), "YouTube", _form.Youtube, _initialForm.Youtube);
        AddSocialError(errors, nameof(EditProfileDTO.Instagram), "Instagram", _form.Instagram, _initialForm.Instagram);
        AddSocialError(errors, nameof(EditProfileDTO.Steam), "Steam", _form.Steam, _initialForm.Steam);
        AddSocialError(errors, nameof(EditProfileDTO.Twitch), "Twitch", _form.Twitch, _initialForm.Twitch);
        AddSocialError(errors, nameof(EditProfileDTO.Github), "GitHub", _form.Github, _initialForm.Github);
        AddSocialError(errors, nameof(EditProfileDTO.NintendoNetwork), "Nintendo Network", _form.NintendoNetwork, _initialForm.NintendoNetwork);
        return errors;
    }

    private static void AddSocialError(
        IDictionary<string, string> errors,
        string key,
        string platform,
        string current,
        string original)
    {
        if (string.Equals(current, original, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(current))
        {
            return;
        }

        if (!SocialProfileLinkRules.TryNormalize(platform, current, out _, out var error))
        {
            errors[key] = error;
        }
    }

    private static void AddSocialIfChanged(
        IDictionary<string, string> changed,
        string key,
        string platform,
        string current,
        string original)
    {
        if (string.Equals(current, original, StringComparison.Ordinal))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(current))
        {
            changed[key] = string.Empty;
            return;
        }

        if (SocialProfileLinkRules.TryNormalize(platform, current, out var normalized, out _))
        {
            changed[key] = normalized;
        }
    }

    private static void AddIfChanged(
        IDictionary<string, string> changed,
        string key,
        string current,
        string original)
    {
        if (!string.Equals(current, original, StringComparison.Ordinal))
        {
            changed[key] = current;
        }
    }

    private void ApplyDiscordFeedback()
    {
        switch (DiscordResult)
        {
            case "connected":
                _feedback = "Your Discord account was connected successfully.";
                _saveSucceeded = true;
                break;

            case "authorization-denied":
                _feedback = "Discord authorization was cancelled. Your account was not changed.";
                _saveSucceeded = false;
                break;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        if (_profileModule is not null)
        {
            try
            {
                await _profileModule.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The circuit is already closed; browser cleanup is no longer possible.
            }
            catch (Exception exception) when (exception is JSException or OperationCanceledException)
            {
                // Cleanup must tolerate a disconnected browser or interrupted interop call.
            }
        }
        _lifetime.Dispose();
    }

    private sealed class ProfileEditForm
    {
        public string UsernameColor { get; set; } = "#b14cff";
        public string Bio { get; set; } = string.Empty;
        public string Patreon { get; set; } = string.Empty;
        public string Paypal { get; set; } = string.Empty;
        public string BuyMeACoffee { get; set; } = string.Empty;
        public string Twitter { get; set; } = string.Empty;
        public string Facebook { get; set; } = string.Empty;
        public string Youtube { get; set; } = string.Empty;
        public string Instagram { get; set; } = string.Empty;
        public string Steam { get; set; } = string.Empty;
        public string Twitch { get; set; } = string.Empty;
        public string Github { get; set; } = string.Empty;
        public string NintendoNetwork { get; set; } = string.Empty;
        public string EmailVisibilityId { get; set; } = string.Empty;
        public string TimeZoneId { get; set; } = string.Empty;

        public static ProfileEditForm From(UserInfo profile) => new()
        {
            UsernameColor = (profile.UserSummary?.UsernameColor ?? profile.UsernameColor) is { Length: > 0 } color
                ? color : "#b14cff",
            Bio = profile.Bio ?? string.Empty,
            Patreon = profile.Patreon ?? string.Empty,
            Paypal = profile.Paypal ?? string.Empty,
            BuyMeACoffee = profile.BuyMeACoffee ?? string.Empty,
            Twitter = profile.Twitter ?? string.Empty,
            Facebook = profile.Facebook ?? string.Empty,
            Youtube = profile.Youtube ?? string.Empty,
            Instagram = profile.Instagram ?? string.Empty,
            Steam = profile.Steam ?? string.Empty,
            Twitch = profile.Twitch ?? string.Empty,
            Github = profile.Github ?? string.Empty,
            NintendoNetwork = profile.NintendoNetwork ?? string.Empty,
            EmailVisibilityId = profile.EmailVisibilityId.ToString(CultureInfo.InvariantCulture),
            TimeZoneId = profile.TimeZoneId.ToString(CultureInfo.InvariantCulture)
        };

        public ProfileEditForm Copy() => (ProfileEditForm)MemberwiseClone();
    }
}
