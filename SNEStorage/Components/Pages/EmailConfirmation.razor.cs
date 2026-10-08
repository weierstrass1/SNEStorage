using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Pages;

[AllowAnonymous]
public partial class EmailConfirmation : IDisposable
{
    private CancellationTokenSource? _attempt;
    private string? _activeUserId;
    private string? _activeToken;
    private bool _requestStarted;
    private ConfirmationState _state = ConfirmationState.Checking;

    [Parameter]
    public string? UserId { get; set; }

    [Parameter, SupplyParameterFromQuery(Name = "token")]
    public string? Token { get; set; }

    [Inject]
    private HttpClient Http { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    protected bool IsChecking => _state == ConfirmationState.Checking;
    protected string StateClass => _state == ConfirmationState.Confirmed ? "success" : IsChecking ? "pending" : "error";

    protected string Heading => _state switch
    {
        ConfirmationState.Confirmed => "Email confirmed!",
        ConfirmationState.Incomplete => "This link is incomplete",
        ConfirmationState.Invalid => "This link could not be verified",
        ConfirmationState.Unavailable => "Confirmation unavailable",
        _ => "Confirming your email"
    };

    protected string Message => _state switch
    {
        ConfirmationState.Confirmed => "Your email address has been verified. You can now log in to SNEStorage.",
        ConfirmationState.Incomplete => "Open the complete confirmation link from your email to verify your account.",
        ConfirmationState.Invalid => "This confirmation link is invalid or has expired. Use your latest confirmation email. If you have already confirmed your email, you can log in.",
        ConfirmationState.Unavailable => "We could not verify your email right now. Please try again in a moment.",
        _ => "Please wait while we verify your confirmation link."
    };

    protected override void OnParametersSet()
    {
        if (_attempt is not null && _activeUserId == UserId && _activeToken == Token)
        {
            return;
        }

        _attempt?.Cancel();
        _attempt?.Dispose();
        _attempt = new CancellationTokenSource();
        _activeUserId = UserId;
        _activeToken = Token;
        _requestStarted = false;
        _state = string.IsNullOrWhiteSpace(UserId) || string.IsNullOrWhiteSpace(Token)
            ? ConfirmationState.Incomplete
            : ConfirmationState.Checking;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        // Start after interactive rendering so prerendering does not confirm twice.
        if (IsChecking && !_requestStarted)
        {
            await ConfirmAsync();
        }
    }

    protected async Task RetryAsync()
    {
        if (_state != ConfirmationState.Unavailable)
        {
            return;
        }

        _state = ConfirmationState.Checking;
        await ConfirmAsync();
    }

    private async Task ConfirmAsync()
    {
        _requestStarted = true;
        var cancellationToken = _attempt!.Token;
        var relativeUrl = $"api/register/confirmed/{Uri.EscapeDataString(_activeUserId!)}?token={Uri.EscapeDataString(_activeToken!)}";
        var endpoint = new Uri(new Uri(Navigation.BaseUri), relativeUrl);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var response = await Http.SendAsync(request, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            var isHtmlPage = response.Content.Headers.ContentType?.MediaType == "text/html";
            var wasRedirected = response.RequestMessage?.RequestUri is { } finalUri && finalUri.AbsolutePath != endpoint.AbsolutePath;
            _state = isHtmlPage || wasRedirected
                ? ConfirmationState.Unavailable
                : response.StatusCode switch
                {
                    HttpStatusCode.OK or HttpStatusCode.NoContent => ConfirmationState.Confirmed,
                    HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.Gone or HttpStatusCode.UnprocessableEntity => ConfirmationState.Invalid,
                    _ => ConfirmationState.Unavailable
                };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            _state = ConfirmationState.Unavailable;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        StateHasChanged();
        if (_state == ConfirmationState.Confirmed)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
                Navigation.NavigateTo("/Login?emailConfirmed=1", forceLoad: true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Leaving the page cancels the pending redirect.
            }
        }
    }

    public void Dispose()
    {
        _attempt?.Cancel();
        _attempt?.Dispose();
        _attempt = null;
    }

    private enum ConfirmationState { Checking, Confirmed, Incomplete, Invalid, Unavailable }
}
