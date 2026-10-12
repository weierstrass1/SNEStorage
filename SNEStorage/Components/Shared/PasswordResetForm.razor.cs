using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Shared;

public partial class PasswordResetForm : IAsyncDisposable
{
    private readonly string _formId = $"password-reset-{Guid.NewGuid():N}";
    private readonly CancellationTokenSource _lifetime = new();
    private readonly EmailForm _emailForm = new();
    private readonly ChangePasswordDTO _passwordForm = new()
    {
        UserId = "", Token = "", NewPassword = "", ConfirmPassword = ""
    };
    private IJSObjectReference? _module;
    private ErrorCopy? _error;
    private bool _saving, _succeeded, _showPasswords, _disposed;

    [Parameter] public bool RequestEmail { get; set; }
    [Parameter] public string? UserId { get; set; }
    [Parameter] public string? Token { get; set; }
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;

    protected object FormModel => RequestEmail ? _emailForm : _passwordForm;
    protected bool HasCompleteLink => RequestEmail ||
        !string.IsNullOrWhiteSpace(UserId) && !string.IsNullOrWhiteSpace(Token);
    protected string PasswordInputType => _showPasswords ? "text" : "password";
    protected string Heading => RequestEmail ? "Reset your password" : "Set a new password";
    protected string Introduction => RequestEmail
        ? "Enter your account email to receive a password reset link."
        : "Enter your new password twice to complete the change.";

    protected override void OnInitialized()
    {
        _passwordForm.UserId = UserId ?? "";
        _passwordForm.Token = Token ?? "";
    }

    protected void TogglePasswords() => _showPasswords = !_showPasswords;
    protected void TryAnotherEmail() { _succeeded = false; _error = null; }

    protected async Task SubmitAsync()
    {
        if (_saving || _disposed || !HasCompleteLink) return;
        _saving = true;
        _error = null;
        try
        {
            var module = _module ?? await JavaScript.InvokeAsync<IJSObjectReference>("import", _lifetime.Token, "./js/password-reset.js?v=2");
            if (_disposed) { await module.DisposeAsync(); return; }
            _module = module;
            var response = await _module.InvokeAsync<PasswordResponse>("submitPassword", _lifetime.Token, _formId, RequestEmail);
            if (_disposed) return;
            _succeeded = response.Status is >= 200 and < 300 &&
                (ApiErrorPresentation.IsSuccess(response.Body, "account") ||
                 ApiErrorPresentation.IsSuccess(response.Body, "email"));
            if (_succeeded)
            {
                _passwordForm.NewPassword = "";
                _passwordForm.ConfirmPassword = "";
                _showPasswords = false;
            }
            else
            {
                _error = response.Status is 0 or 408 or 499 or 504 or (>= 200 and < 300)
                    ? new("Response unavailable", "We could not confirm the result. Check your email or try logging in before submitting again. Your information has been kept.")
                    : ApiErrorPresentation.Describe(response.Status, response.Body, RequestEmail ? "account" : "password-reset");
            }
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException)
        {
            if (!_disposed) _error = new("Request interrupted", "We could not confirm the result. Check your email or try logging in before submitting again. Your information has been kept.");
        }
        finally { _saving = false; }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _lifetime.Cancel();
        _passwordForm.NewPassword = "";
        _passwordForm.ConfirmPassword = "";
        _passwordForm.Token = "";
        if (_module is not null)
        {
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _module.InvokeVoidAsync("cancelPassword", cleanup.Token, _formId);
                await _module.DisposeAsync();
            }
            catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
        }
        _lifetime.Dispose();
    }

    private sealed class EmailForm
    {
        [Required(ErrorMessage = "Enter your account email.")]
        [EmailAddress(ErrorMessage = "Enter a valid email address.")]
        public string Email { get; set; } = "";
    }

    private sealed class PasswordResponse
    {
        public int Status { get; set; }
        public string? Body { get; set; }
    }
}
