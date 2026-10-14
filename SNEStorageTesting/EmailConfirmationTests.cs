using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using SNEStorage.Components.Pages;
using SNEStorage.Controllers;

namespace SNEStorageTesting;

public class EmailConfirmationTests
{
    [Fact]
    public void ConfirmationRequestMatchesTheAnonymousControllerRouteAndBindings()
    {
        var action = typeof(RegisterController).GetMethod(nameof(RegisterController.Confirmed))!;
        Assert.Equal("api/register", typeof(RegisterController).GetCustomAttribute<Microsoft.AspNetCore.Mvc.RouteAttribute>()!.Template);
        Assert.Equal("confirmed/{userId}", action.GetCustomAttribute<HttpPostAttribute>()!.Template);
        Assert.NotNull(action.GetCustomAttribute<AllowAnonymousAttribute>());

        var userId = action.GetParameters().Single(parameter => parameter.Name == "userId");
        var token = action.GetParameters().Single(parameter => parameter.Name == "token");
        Assert.NotNull(userId.GetCustomAttribute<FromRouteAttribute>());
        var query = Assert.IsType<FromQueryAttribute>(token.GetCustomAttribute<FromQueryAttribute>());
        Assert.Equal("token", query.Name ?? token.Name);
    }

    [Theory]
    [InlineData("test-user", null)]
    [InlineData("test-user", "")]
    [InlineData("test-user", "   ")]
    [InlineData(null, "confirmation-code")]
    [InlineData("", "confirmation-code")]
    public async Task IncompleteLinksDoNotCallTheController(string? userId, string? code)
    {
        await using var page = await RenderedPage.CreateAsync(HttpStatusCode.OK, userId, code);
        await page.StartAsync();

        Assert.Equal(0, page.Module.RequestCount);
        Assert.Equal(0, page.JavaScript.ImportCount);
        Assert.Contains("This link is incomplete", await page.HtmlAsync());
        Assert.Null(page.Navigation.LastDestination);
    }

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.NoContent)]
    public async Task ConfirmationUsesTheControllerAndRedirectsOnlyAfterSuccess(HttpStatusCode status)
    {
        await using var page = await RenderedPage.CreateAsync(status, "test-user", "code_with-URL-safe+characters=");
        var startedAt = System.Diagnostics.Stopwatch.StartNew();
        await page.StartAsync();

        Assert.Equal("confirmEmail", page.Module.RequestIdentifier);
        Assert.Equal("./js/email-confirmation.js?v=1", page.JavaScript.ImportPath);
        Assert.Equal("https://snestorage.test/api/register/confirmed/test-user?token=code_with-URL-safe%2Bcharacters%3D", page.Module.RequestUrl);
        Assert.Contains("Email confirmed!", await page.HtmlAsync());
        Assert.Equal("/Login?emailConfirmed=1", page.Navigation.LastDestination);
        Assert.True(page.Navigation.ForceLoad);
        Assert.True(startedAt.Elapsed >= TimeSpan.FromSeconds(3));

        await page.StartAsync(firstRender: false);
        Assert.Equal(1, page.Module.RequestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task InvalidOrExpiredLinksShowAnErrorWithoutRedirecting(HttpStatusCode status)
    {
        await using var page = await RenderedPage.CreateAsync(status);
        await page.StartAsync();

        Assert.Contains("This link could not be verified", await page.HtmlAsync());
        Assert.Null(page.Navigation.LastDestination);
    }

    [Theory]
    [InlineData(HttpStatusCode.MethodNotAllowed)]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Accepted)]
    public async Task MissingOrUnavailableActionsNeverDisplayFalseSuccess(HttpStatusCode status)
    {
        await using var page = await RenderedPage.CreateAsync(status);
        await page.StartAsync();

        Assert.Contains("Confirmation unavailable", await page.HtmlAsync());
        Assert.Contains("Try again", await page.HtmlAsync());
        Assert.Null(page.Navigation.LastDestination);
    }

    // The browser module returns 502 for HTML and 0 for failed/redirected fetches.
    [Theory]
    [InlineData(502)]
    [InlineData(0)]
    public async Task BrowserRejectedResponsesAreNotTreatedAsConfirmation(int status)
    {
        await using var page = await RenderedPage.CreateAsync((HttpStatusCode)status);
        await page.StartAsync();

        Assert.Contains("Confirmation unavailable", await page.HtmlAsync());
        Assert.Null(page.Navigation.LastDestination);
    }

    [Fact]
    public async Task LeavingThePageCancelsThePendingLoginRedirect()
    {
        await using var page = await RenderedPage.CreateAsync(HttpStatusCode.OK);
        Task? confirmation = null;
        await page.Renderer.Dispatcher.InvokeAsync(() => { confirmation = page.Capture.Component!.StartAsync(); });

        Assert.Contains("Email confirmed!", await page.HtmlAsync());
        Assert.Null(page.Navigation.LastDestination);
        await page.Renderer.Dispatcher.InvokeAsync(async () => await page.Capture.Component!.DisposeAsync());
        await confirmation!;

        Assert.Null(page.Navigation.LastDestination);
        Assert.Equal(page.Module.RequestKey, page.Module.CancelledKey);
        Assert.True(page.Module.Disposed);
    }

    [Fact]
    public async Task TheUserCanRetryATemporaryFailure()
    {
        await using var page = await RenderedPage.CreateAsync(HttpStatusCode.ServiceUnavailable);
        await page.StartAsync();
        page.Module.Status = HttpStatusCode.BadRequest;
        await page.Renderer.Dispatcher.InvokeAsync(() => page.Capture.Component!.RetryConfirmationAsync());

        Assert.Equal(2, page.Module.RequestCount);
        Assert.Equal(1, page.JavaScript.ImportCount);
        Assert.Contains("This link could not be verified", await page.HtmlAsync());
        Assert.Null(page.Navigation.LastDestination);
    }

    private sealed class RenderedPage : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        private HtmlRootComponent _root;
        public TestJavaScriptRuntime JavaScript { get; }
        public TestConfirmationModule Module => JavaScript.Module;
        public TestNavigationManager Navigation { get; } = new();
        public ComponentCapture Capture { get; } = new();
        public HtmlRenderer Renderer { get; }

        private RenderedPage(HttpStatusCode status)
        {
            JavaScript = new TestJavaScriptRuntime(new TestConfirmationModule { Status = status });
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<NavigationManager>(Navigation);
            services.AddSingleton(Capture);
            services.AddSingleton<IJSRuntime>(JavaScript);
            _services = services.BuildServiceProvider();
            Renderer = new HtmlRenderer(_services, _services.GetRequiredService<ILoggerFactory>());
        }

        public static async Task<RenderedPage> CreateAsync(HttpStatusCode status, string? userId = "test-user", string? code = "confirmation-code")
        {
            var page = new RenderedPage(status);
            page._root = await page.Renderer.Dispatcher.InvokeAsync(() => page.Renderer.RenderComponentAsync<ConfirmationFixture>(
                ParameterView.FromDictionary(new Dictionary<string, object?> { [nameof(EmailConfirmation.UserId)] = userId, [nameof(EmailConfirmation.Token)] = code })));
            return page;
        }

        public Task StartAsync(bool firstRender = true) => Renderer.Dispatcher.InvokeAsync(() => Capture.Component!.StartAsync(firstRender));
        public Task<string> HtmlAsync() => Renderer.Dispatcher.InvokeAsync(() => _root.ToHtmlString());

        public async ValueTask DisposeAsync()
        {
            await Renderer.DisposeAsync();
            await _services.DisposeAsync();
        }
    }

    private sealed class ComponentCapture { public ConfirmationFixture? Component { get; set; } }

    private sealed class ConfirmationFixture : EmailConfirmation
    {
        [Inject]
        public ComponentCapture Capture { get; set; } = default!;

        protected override void OnInitialized() => Capture.Component = this;
        public Task StartAsync(bool firstRender = true) => OnAfterRenderAsync(firstRender);
        public Task RetryConfirmationAsync() => RetryAsync();
    }

    private sealed class TestJavaScriptRuntime(TestConfirmationModule module) : IJSRuntime
    {
        public TestConfirmationModule Module { get; } = module;
        public int ImportCount { get; private set; }
        public string? ImportPath { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,
            CancellationToken cancellationToken, object?[]? args)
        {
            Assert.Equal("import", identifier);
            ImportCount++;
            ImportPath = Assert.IsType<string>(Assert.Single(args!));
            return ValueTask.FromResult((TValue)(object)Module);
        }
    }

    private sealed class TestConfirmationModule : IJSObjectReference
    {
        public HttpStatusCode Status { get; set; }
        public int RequestCount { get; private set; }
        public string? RequestIdentifier { get; private set; }
        public string? RequestKey { get; private set; }
        public string? RequestUrl { get; private set; }
        public string? CancelledKey { get; private set; }
        public bool Disposed { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier,
            CancellationToken cancellationToken, object?[]? args)
        {
            if (identifier == "cancelConfirmation")
            {
                CancelledKey = Assert.IsType<string>(Assert.Single(args!));
                return ValueTask.FromResult(default(TValue)!);
            }

            Assert.Equal("confirmEmail", identifier);
            Assert.Equal(2, args!.Length);
            RequestCount++;
            RequestIdentifier = identifier;
            RequestKey = Assert.IsType<string>(args[0]);
            RequestUrl = Assert.IsType<string>(args[1]);
            return ValueTask.FromResult((TValue)(object)(int)Status);
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestNavigationManager : NavigationManager
    {
        public TestNavigationManager() => Initialize("https://snestorage.test/", "https://snestorage.test/Register/Confirmation/test-user");
        public string? LastDestination { get; private set; }
        public bool ForceLoad { get; private set; }

        protected override void NavigateToCore(string uri, NavigationOptions options)
        {
            LastDestination = uri;
            ForceLoad = options.ForceLoad;
        }
    }
}
