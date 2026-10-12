using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace SNEStorage.Components.Shared;

public partial class ConfirmationEmailForm : IAsyncDisposable
{
    private readonly string _requestKey = $"confirmation-resend-{Guid.NewGuid():N}";
    private readonly CancellationTokenSource _lifetime = new();
    private readonly EmailForm _form = new();
    private IJSObjectReference? _module;
    private ErrorCopy? _error;
    private bool _saving, _succeeded, _disposed;
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;

    protected async Task SubmitAsync()
    {
        if (_saving || _disposed) return;
        _saving = true;
        _succeeded = false;
        _error = null;
        try
        {
            var module = _module ?? await JavaScript.InvokeAsync<IJSObjectReference>(
                "import", _lifetime.Token, "./js/confirmation-email.js?v=1");
            if (_disposed) { await module.DisposeAsync(); return; }
            _module = module;
            var response = await _module.InvokeAsync<EmailResponse>(
                "resendConfirmation", _lifetime.Token, _requestKey, _form.Email);
            if (_disposed) return;
            _succeeded = response.Status is >= 200 and < 300 &&
                (ApiErrorPresentation.IsSuccess(response.Body, "account") ||
                 ApiErrorPresentation.IsSuccess(response.Body, "email"));
            if (!_succeeded)
                _error = response.Status is 0 or 408 or 499 or 504 or (>= 200 and < 300)
                    ? new("Response unavailable", "We could not confirm the result. Check your inbox before trying again. Your email has been kept.")
                    : ApiErrorPresentation.Describe(response.Status, response.Body, "account");
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException)
        {
            if (!_disposed) _error = new("Request interrupted", "We could not confirm the result. Check your inbox before trying again. Your email has been kept.");
        }
        finally { _saving = false; }
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
                await _module.InvokeVoidAsync("cancelResend", cleanup.Token, _requestKey);
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

    private sealed class EmailResponse
    {
        public int Status { get; set; }
        public string? Body { get; set; }
    }
}
