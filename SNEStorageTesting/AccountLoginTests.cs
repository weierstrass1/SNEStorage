using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using SNEStorage.Controllers;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorageTesting;

public class AccountLoginTests
{
    [Fact(DisplayName = "Login distinguishes an unconfirmed email from invalid credentials")]
    public async Task Login_ReturnsEmailNotConfirmed_WhenEmailIsUnconfirmed()
    {
        await using var environment = new LoginTestEnvironment();
        await environment.CreateUser("unconfirmed", emailConfirmed: false);

        var result = await environment.AccountService.Login(new LoginCredentials
        {
            User = "unconfirmed",
            Password = LoginTestEnvironment.ValidPassword
        });

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.EmailNotConfirmed, result.AsT1.Result);
    }

    [Fact(DisplayName = "Login reports a locked account before treating it as a bad password")]
    public async Task Login_ReturnsAccountLockedOut_WhenLockoutIsActive()
    {
        await using var environment = new LoginTestEnvironment();
        var user = await environment.CreateUser("locked", emailConfirmed: true);
        Assert.True((await environment.UserManager.SetLockoutEndDateAsync(
            user, DateTimeOffset.UtcNow.AddMinutes(10))).Succeeded);

        var result = await environment.AccountService.Login(new LoginCredentials
        {
            User = "locked",
            Password = LoginTestEnvironment.ValidPassword
        });

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.AccountLockedOut, result.AsT1.Result);
    }

    [Fact(DisplayName = "Login still reports InvalidPassword for ordinary credential failures")]
    public async Task Login_ReturnsInvalidPassword_WhenPasswordIsWrong()
    {
        await using var environment = new LoginTestEnvironment();
        await environment.CreateUser("confirmed", emailConfirmed: true);

        var result = await environment.AccountService.Login(new LoginCredentials
        {
            User = "confirmed",
            Password = "Wrong1!Password"
        });

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.InvalidPassword, result.AsT1.Result);
    }

    [Fact(DisplayName = "LoginController includes the precise login failure and preserves a local return URL")]
    public async Task LoginController_RedirectsWithSpecificError_AndLocalReturnUrl()
    {
        await using var environment = new LoginTestEnvironment();
        await environment.CreateUser("unconfirmed", emailConfirmed: false);
        var controller = environment.CreateController();
        const string returnUrl = "/Resource/15?tab=versions";

        var result = await controller.Login(new LoginCredentials
        {
            User = "unconfirmed",
            Password = LoginTestEnvironment.ValidPassword
        }, returnUrl);

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Login?result=EmailNotConfirmed&returnUrl=" +
            Uri.EscapeDataString(returnUrl), redirect.Url);
    }

    [Fact(DisplayName = "LoginController replaces an external return URL with the site root")]
    public async Task LoginController_RejectsExternalReturnUrl()
    {
        await using var environment = new LoginTestEnvironment();
        await environment.CreateUser("unconfirmed", emailConfirmed: false);
        var controller = environment.CreateController();

        var result = await controller.Login(new LoginCredentials
        {
            User = "unconfirmed",
            Password = LoginTestEnvironment.ValidPassword
        }, "https://evil.example/steal");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/Login?result=EmailNotConfirmed&returnUrl=%2F", redirect.Url);
    }

    private sealed class LoginTestEnvironment : IAsyncDisposable
    {
        public const string ValidPassword = "Valid1!Password";

        private readonly ServiceProvider _provider;
        private readonly AsyncServiceScope _scope;
        private readonly DefaultHttpContext _httpContext;
        public UserManager<ApplicationUser> UserManager { get; }
        public AccountService AccountService { get; }

        public LoginTestEnvironment()
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddHttpContextAccessor();
            services.AddDbContext<SnestorageContext>(options =>
                options.UseInMemoryDatabase($"login-tests-{Guid.NewGuid():N}"));
            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
                {
                    options.SignIn.RequireConfirmedEmail = true;
                    options.Lockout.AllowedForNewUsers = true;
                    options.Lockout.MaxFailedAccessAttempts = 5;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
                })
                .AddEntityFrameworkStores<SnestorageContext>()
                .AddDefaultTokenProviders();

            _provider = services.BuildServiceProvider();
            _scope = _provider.CreateAsyncScope();
            var scopedServices = _scope.ServiceProvider;
            var accessor = scopedServices.GetRequiredService<IHttpContextAccessor>();
            _httpContext = new DefaultHttpContext { RequestServices = scopedServices };
            accessor.HttpContext = _httpContext;

            UserManager = scopedServices.GetRequiredService<UserManager<ApplicationUser>>();
            var context = scopedServices.GetRequiredService<SnestorageContext>();
            var dbHelper = new DBHelperService(context, NullLogger<DBHelperService>.Instance);
            AccountService = new AccountService(
                UserManager,
                null!,
                null!,
                scopedServices.GetRequiredService<SignInManager<ApplicationUser>>(),
                context,
                null!,
                accessor,
                null!,
                dbHelper,
                null!,
                NullLogger<AccountService>.Instance);
        }

        public async Task<ApplicationUser> CreateUser(string username, bool emailConfirmed)
        {
            ApplicationUser user = new()
            {
                UserName = username,
                Email = $"{username}@example.com",
                EmailConfirmed = emailConfirmed,
                LockoutEnabled = true
            };

            var result = await UserManager.CreateAsync(user, ValidPassword);
            Assert.True(result.Succeeded,
                string.Join(", ", result.Errors.Select(error => error.Description)));
            return user;
        }

        public LoginController CreateController()
        {
            var actionContext = new ActionContext(_httpContext, new RouteData(),
                new ControllerActionDescriptor());
            return new LoginController(AccountService)
            {
                ControllerContext = new ControllerContext(actionContext),
                Url = new UrlHelper(actionContext)
            };
        }

        public async ValueTask DisposeAsync()
        {
            await _scope.DisposeAsync();
            await _provider.DisposeAsync();
        }
    }
}
