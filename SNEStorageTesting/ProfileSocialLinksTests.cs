using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using SNEStorage.Components.Pages;
using SNEStorage.Models;

namespace SNEStorageTesting;

public class ProfileSocialLinksTests
{
    [Theory]
    [InlineData("Patreon", "creator", "https://www.patreon.com/creator")]
    [InlineData("Patreon", "patreon.com/c/creator", "https://patreon.com/c/creator")]
    [InlineData("Patreon", "https://www.patreon.com/cw/creator", "https://www.patreon.com/cw/creator")]
    [InlineData("Patreon", "https://www.patreon.com/c/creator/about?utm_source=share#about", "https://www.patreon.com/c/creator")]
    [InlineData("Patreon", "https://www.patreon.com/user?u=123456&fan_landing=true", "https://www.patreon.com/user?u=123456")]
    [InlineData("Patreon", "https://patreon.com/creator/posts?utm_campaign=share", "https://patreon.com/creator")]
    [InlineData("Paypal", "creator", "https://www.paypal.me/creator")]
    [InlineData("Paypal", "http://paypal.me/creator?locale.x=en_US", "https://paypal.me/creator")]
    [InlineData("Paypal", "https://www.paypal.com/paypalme/creator", "https://www.paypal.com/paypalme/creator")]
    [InlineData("BuyMeACoffee", "buymeacoffee.com/creator?utm_source=share", "https://buymeacoffee.com/creator")]
    [InlineData("Discord", "https://discord.gg/AbC123", "https://discord.gg/AbC123")]
    [InlineData("Discord", "https://discord.com/invite/AbC123?utm_source=share", "https://discord.com/invite/AbC123")]
    [InlineData("Discord", "https://discord.com/users/123456789012345678", "https://discord.com/users/123456789012345678")]
    [InlineData("Twitter", "@creator", "https://x.com/creator")]
    [InlineData("Twitter", "https://twitter.com/creator?s=21", "https://twitter.com/creator")]
    [InlineData("Facebook", "https://www.facebook.com/creator?mibextid=share", "https://www.facebook.com/creator")]
    [InlineData("Facebook", "https://m.facebook.com/profile.php?id=123456&ref=share", "https://m.facebook.com/profile.php?id=123456")]
    [InlineData("Facebook", "https://www.facebook.com/people/Creator-Name/123456/", "https://www.facebook.com/people/Creator-Name/123456")]
    [InlineData("Youtube", "@creator", "https://www.youtube.com/@creator")]
    [InlineData("Youtube", "https://www.youtube.com/@creator?si=share", "https://www.youtube.com/@creator")]
    [InlineData("Youtube", "https://www.youtube.com/channel/UC123456789", "https://www.youtube.com/channel/UC123456789")]
    [InlineData("Instagram", "https://www.instagram.com/creator.name/?igsh=share", "https://www.instagram.com/creator.name")]
    [InlineData("Steam", "steamcommunity.com/id/creator", "https://steamcommunity.com/id/creator")]
    [InlineData("Steam", "https://steamcommunity.com/profiles/76561198000000000/", "https://steamcommunity.com/profiles/76561198000000000")]
    [InlineData("Twitch", "https://www.twitch.tv/creator/about?tt_content=share", "https://www.twitch.tv/creator")]
    [InlineData("Twitch", "https://www.twitch.tv/abc", "https://www.twitch.tv/abc")]
    [InlineData("Github", "https://github.com/creator-name?tab=repositories", "https://github.com/creator-name")]
    public void SupportedProfileFormatsProduceAbsoluteClickableLinks(string field, string value, string expected)
    {
        var link = new ProfileFixture(field, value).Links.Single();

        Assert.Equal(expected, link.Href);
        Assert.DoesNotContain("Link format needs updating", link.DisplayText);
    }

    [Theory]
    [InlineData("Patreon", "https://patreon.com.evil.example/c/creator")]
    [InlineData("Patreon", "https://patreon.com@evil.example/creator")]
    [InlineData("Patreon", "https://user:password@patreon.com/c/creator")]
    [InlineData("Patreon", "https://patreon.com:444/c/creator")]
    [InlineData("Patreon", "https://patreon.com/user?u=not-a-number")]
    [InlineData("Patreon", "https://patreon.com/user?u=123&u=456")]
    [InlineData("Patreon", "https://patreon.com/c/")]
    [InlineData("Patreon", "https://patreon.com/posts/some-post")]
    [InlineData("Paypal", "https://evil.example/creator")]
    [InlineData("BuyMeACoffee", "https://patreon.com/creator")]
    [InlineData("Discord", "https://discord.com/users/not-an-id")]
    [InlineData("Twitter", "https://x.com/creator-name")]
    [InlineData("Facebook", "https://facebook.com/profile.php?id=not-an-id")]
    [InlineData("Youtube", "https://youtube.com/watch?v=video")]
    [InlineData("Instagram", "https://instagram.com/creator..name")]
    [InlineData("Steam", "https://steamcommunity.com/profiles/not-an-id")]
    [InlineData("Twitch", "https://twitch.tv/creator/video/123")]
    [InlineData("Github", "https://github.com/creator--name")]
    [InlineData("Patreon", "javascript:alert(1)")]
    [InlineData("Patreon", "ftp://patreon.com/creator")]
    public void InvalidLinksCannotBecomeClickable(string field, string value)
    {
        var link = new ProfileFixture(field, value).Links.Single();

        Assert.Null(link.Href);
        Assert.Equal("Link format needs updating", link.DisplayText);
    }

    [Fact]
    public void NintendoNetworkIsDisplayedAsAnIdWithoutAHyperlink()
    {
        var link = new ProfileFixture("NintendoNetwork", "player_1").Links.Single();

        Assert.Null(link.Href);
        Assert.Equal("player_1", link.DisplayText);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptySocialFieldsDoNotProduceCards(string? value)
    {
        Assert.Empty(new ProfileFixture("Patreon", value).Links);
    }

    [Fact]
    public async Task ProfileRendersPatreonAsAnExternalAnchorWithItsLocalIcon()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<NavigationManager, TestNavigationManager>();
        services.AddSingleton<AuthenticationStateProvider, TestAuthenticationStateProvider>();
        services.AddSingleton<IJSRuntime, NoJavaScriptRuntime>();
        await using var serviceProvider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(serviceProvider, serviceProvider.GetRequiredService<ILoggerFactory>());

        var html = await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<RenderedProfileFixture>();
            return component.ToHtmlString();
        });

        Assert.Contains("href=\"https://www.patreon.com/c/creator\"", html);
        Assert.Contains("target=\"_blank\" rel=\"noopener noreferrer\"", html);
        Assert.Contains("href=\"/images/social-icons.svg#patreon\"", html);
        Assert.DoesNotContain("Link format needs updating", html);
    }

    // Supply controller response data without a database or an authenticated browser session.
    private sealed class ProfileFixture : Profile
    {
        public ProfileFixture(string field, string? value)
        {
            var profile = new UserInfo { UserId = "test-user" };
            typeof(UserInfo).GetProperty(field)!.SetValue(profile, value);
            typeof(Profile).GetField("_profile", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(this, profile);
        }

        public IReadOnlyList<(string? Href, string DisplayText)> Links =>
            SocialFields.Select(link => (link.Href, link.DisplayText)).ToArray();
    }

    private sealed class RenderedProfileFixture : Profile
    {
        public RenderedProfileFixture()
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(Profile).GetField("_profile", flags)!.SetValue(this, new UserInfo
            {
                UserId = "test-user",
                Patreon = "https://www.patreon.com/c/creator?utm_source=share"
            });
            typeof(Profile).GetField("_isLoading", flags)!.SetValue(this, false);
            typeof(Profile).GetField("_isAuthenticated", flags)!.SetValue(this, true);
        }

        protected override Task OnAfterRenderAsync(bool firstRender) => Task.CompletedTask;
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("https://snestorage.test/", "https://snestorage.test/Profile");
    }

    private sealed class TestAuthenticationStateProvider : AuthenticationStateProvider
    {
        public override Task<AuthenticationState> GetAuthenticationStateAsync() =>
            Task.FromResult(new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    private sealed class NoJavaScriptRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new InvalidOperationException("This rendering test must not call JavaScript or the profile API.");

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) =>
            InvokeAsync<TValue>(identifier, args);
    }
}
