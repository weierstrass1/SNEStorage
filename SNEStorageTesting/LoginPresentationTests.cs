using SNEStorage.Components.Pages;

namespace SNEStorageTesting;

public class LoginPresentationTests
{
    [Theory(DisplayName = "The login page explains each server-side authentication result")]
    [InlineData("invalid", "Login failed. Check your username and password.")]
    [InlineData("InvalidPassword", "Login failed. Check your username and password.")]
    [InlineData("UserNotFound", "Login failed. Check your username and password.")]
    [InlineData("rate-limited", "Too many login attempts. Please wait a moment before trying again.")]
    [InlineData("AccountLockedOut", "Your account is temporarily locked after too many failed login attempts. Try again in 10 minutes.")]
    [InlineData("EmailNotConfirmed", "Confirm your email before logging in. Check your inbox or use the confirmation email link below.")]
    [InlineData("DBError", "Login is temporarily unavailable. Please try again later.")]
    [InlineData("CancellationTokenError", "Login could not finish. Please try again.")]
    [InlineData("UnexpectedResult", "Login could not be completed. Please try again.")]
    public void StatusMessage_DescribesLoginResult(string result, string expected)
    {
        var page = new LoginFixture();
        page.SetResult(result);

        Assert.Equal(expected, page.Message);
    }

    [Theory(DisplayName = "Login return URLs keep local destinations and reject unsafe or recursive destinations")]
    [InlineData("/Resource/15?tab=versions", "/Resource/15?tab=versions")]
    [InlineData("~/Resources", "/Resources")]
    [InlineData("https://evil.example/steal", "/")]
    [InlineData("//evil.example/steal", "/")]
    [InlineData("/Login", "/")]
    [InlineData("/Register/Confirmation", "/")]
    [InlineData(null, "/")]
    public void LoginReturnUrl_NormalizesDestination(string? returnUrl, string expected)
    {
        var page = new LoginFixture();
        page.SetReturnUrl(returnUrl);

        Assert.Equal(expected, page.NormalizedReturnUrl);
        Assert.Equal("/api/login/submit?returnUrl=" + Uri.EscapeDataString(expected),
            page.Action);
    }

    [Fact(DisplayName = "Successful account messages take precedence over an old login error")]
    public void StatusMessage_PrioritizesSuccessfulAccountState()
    {
        var page = new LoginFixture();
        page.SetStatus("InvalidPassword", emailConfirmed: "1");

        Assert.Equal("Your email has been confirmed. You can now log in.", page.Message);
    }

    private sealed class LoginFixture : Login
    {
        public string? Message => StatusMessage;
        public string NormalizedReturnUrl => LoginReturnUrl;
        public string Action => LoginAction;
        public void SetResult(string? result) => Result = result;
        public void SetReturnUrl(string? returnUrl) => ReturnUrl = returnUrl;

        public void SetStatus(string? result, string? emailConfirmed = null)
        {
            Result = result;
            EmailConfirmed = emailConfirmed;
        }
    }
}
