using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.Web.HtmlRendering;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SNEStorage.Components.Pages;
using SNEStorage.Controllers;

namespace SNEStorageTesting;

public class EmailConfirmationTests
{
    [Fact]
    public void ConfirmationRequestMatchesTheAnonymousControllerRouteAndBindings()
    {
        var action = typeof(RegisterController).GetMethod(nameof(RegisterController.Confirmed))!;
        Assert.Equal("/Register/Confirmed/{userId}", action.GetCustomAttribute<HttpPostAttribute>()!.Template);
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

        Assert.Equal(0, page.Handler.RequestCount);
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

        Assert.Equal(HttpMethod.Post, page.Handler.Method);
        Assert.Equal("https://snestorage.test/Register/Confirmed/test-user?token=code_with-URL-safe%2Bcharacters%3D", page.Handler.RequestUri!.AbsoluteUri);
        Assert.Contains("Email confirmed!", await page.HtmlAsync());
        Assert.Equal("/Login?emailConfirmed=1", page.Navigation.LastDestination);
        Assert.True(page.Navigation.ForceLoad);
        Assert.True(startedAt.Elapsed >= TimeSpan.FromSeconds(3));

        await page.StartAsync(firstRender: false);
        Assert.Equal(1, page.Handler.RequestCount);
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

    [Fact]
    public async Task AnHtmlResponseIsNotTreatedAsConfirmation()
    {
        await using var page = await RenderedPage.CreateAsync(HttpStatusCode.OK);
        page.Handler.ContentType = "text/html";
        await page.StartAsync();

        Assert.Contains("Confirmation unavailable", await page.HtmlAsync());
        Assert.Null(page.Navigation.LastDestination);
    }

    [Fact]
    public async Task AControllerRedirectIsNotTreatedAsConfirmation()
    {
        await using var page = await RenderedPage.CreateAsync(HttpStatusCode.OK);
        page.Handler.FinalUri = new Uri("https://snestorage.test/Login");
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
        await page.Renderer.Dispatcher.InvokeAsync(() => page.Capture.Component!.Dispose());
        await confirmation!;

        Assert.Null(page.Navigation.LastDestination);
    }

    [Fact]
    public async Task TheUserCanRetryATemporaryFailure()
    {
        await using var page = await RenderedPage.CreateAsync(HttpStatusCode.ServiceUnavailable);
        await page.StartAsync();
        page.Handler.Status = HttpStatusCode.BadRequest;
        await page.Renderer.Dispatcher.InvokeAsync(() => page.Capture.Component!.RetryConfirmationAsync());

        Assert.Equal(2, page.Handler.RequestCount);
        Assert.Contains("This link could not be verified", await page.HtmlAsync());
        Assert.Null(page.Navigation.LastDestination);
    }

    private sealed class RenderedPage : IAsyncDisposable
    {
        private readonly ServiceProvider _services;
        private HtmlRootComponent _root;
        public TestHttpHandler Handler { get; }
        public TestNavigationManager Navigation { get; } = new();
        public ComponentCapture Capture { get; } = new();
        public HtmlRenderer Renderer { get; }

        private RenderedPage(HttpStatusCode status)
        {
            Handler = new TestHttpHandler { Status = status };
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<NavigationManager>(Navigation);
            services.AddSingleton(Capture);
            services.AddSingleton(new HttpClient(Handler) { BaseAddress = new Uri("https://snestorage.test/api/") });
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

    private sealed class TestHttpHandler : HttpMessageHandler
    {
        public HttpStatusCode Status { get; set; }
        public string? ContentType { get; set; }
        public Uri? FinalUri { get; set; }
        public int RequestCount { get; private set; }
        public HttpMethod? Method { get; private set; }
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            Method = request.Method;
            RequestUri = request.RequestUri;
            var response = new HttpResponseMessage(Status) { Content = new StringContent(string.Empty), RequestMessage = request };
            if (ContentType is not null)
            {
                response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(ContentType);
            }
            if (FinalUri is not null)
            {
                response.RequestMessage = new HttpRequestMessage(HttpMethod.Get, FinalUri);
            }
            return Task.FromResult(response);
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
