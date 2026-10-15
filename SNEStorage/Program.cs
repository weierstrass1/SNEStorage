using EFCore.Migrations.CustomSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using SNEStorage.Components;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;
using SNEStorage.Services.QueryComponents;
using System.Globalization;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
IServiceCollection services = builder.Services;
services.AddOptions<DiscordDTO>()
    .Bind(builder.Configuration.GetSection(DiscordDTO.SectionName))
    .Validate(options =>
        !options.Enabled ||
        (
            !string.IsNullOrWhiteSpace(options.ClientSecret) &&
            !string.IsNullOrWhiteSpace(options.PublicBaseUrl) &&
            !string.IsNullOrWhiteSpace(options.BotBaseUrl) &&
            !string.IsNullOrWhiteSpace(options.OAuthCallbackPath) &&
            !string.IsNullOrWhiteSpace(options.InviteUrl) &&
            !string.IsNullOrWhiteSpace(options.BotApiKey)
        ),
        "Discord configuration is incomplete.")
    .ValidateOnStart();
services.AddControllers(options =>
    {
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
    }).AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler =
            ReferenceHandler.IgnoreCycles;
    })
    .AddControllersAsServices();
services.AddRazorComponents()
    .AddInteractiveServerComponents();
services.AddCascadingAuthenticationState();
services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(new Uri(sp.GetRequiredService<NavigationManager>().BaseUri), "api/")
});
services.AddDbContext<SnestorageContext>(options =>
{
    options
        .UseSqlServer(builder.Configuration.GetConnectionString("SNEStorageContext"))
        .UseCustomSql(custom => custom.UseSqlServer());
});
services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8
                .GetBytes(builder.Configuration["JWTKey"]!)),
            ClockSkew = TimeSpan.Zero
        };

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var http = context.HttpContext;
                var accountService = http.RequestServices
                    .GetRequiredService<AccountService>();

                var userId = context.Principal?
                    .FindFirstValue(ClaimTypes.NameIdentifier);
                var stamp = context.Principal?
                    .FindFirstValue("security_stamp");

                var validation = await accountService.ValidateJwt(
                    userId, stamp, http.RequestAborted);

                if (!validation.TryPickT0(out var valid, out var error))
                {
                    http.Items["JwtValidationError"] =
                        $"AccountController-{error.Result.Type}";

                    context.Fail("Account validation unavailable.");
                    return;
                }

                if (!valid)
                {
                    context.Fail("Invalid or revoked token.");
                    return;
                }

                if (http.Items["BrowserJwt"] is not true ||
                    context.SecurityToken.ValidTo > DateTime.UtcNow.AddDays(45))
                    return;

                var username = context.Principal?
                    .FindFirstValue(ClaimTypes.Name);

                if (string.IsNullOrEmpty(username))
                    return;

                var tokenResult = await accountService.BuildToken(username);
                if (!tokenResult.TryPickT0(out var response, out _))
                    return;

                http.Response.Cookies.Append("__Host-SNEStorage.Auth",
                    response.Token, new CookieOptions
                    {
                        HttpOnly = true,
                        Secure = true,
                        SameSite = SameSiteMode.Lax,
                        Path = "/",
                        Expires = new DateTimeOffset(response.ExpireTime)
                    });
            },
            OnChallenge = async context =>
            {
                var http = context.HttpContext;

                if (http.Items["JwtValidationError"] is not string error)
                    return;

                context.HandleResponse();

                if (http.RequestAborted.IsCancellationRequested)
                    return;

                http.Response.StatusCode =
                    StatusCodes.Status503ServiceUnavailable;

                await http.Response.WriteAsJsonAsync(
                    error, cancellationToken: http.RequestAborted);
            }
        };
    });
services.AddAuthorizationBuilder()
    .AddPolicy("Admin", pol => pol.RequireClaim("Admin"))
    .AddPolicy("Moderator", pol => pol.RequireClaim("Moderator"));
services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
        { 
            Title = "Api SNEStorage REST", 
            Version = "v1" 
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header
    });
    c.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
services
    .AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.SignIn.RequireConfirmedEmail = true;

            options.Lockout.AllowedForNewUsers = true;
            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
        })
    .AddEntityFrameworkStores<SnestorageContext>()
    .AddDefaultTokenProviders();
services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromDays(90);
    options.SlidingExpiration = true;
});
services.AddHttpContextAccessor();
services.AddMemoryCache();
services.AddSingleton<CacheService>();
services.AddSingleton<CaptchaFrequencyService>();
services.AddTransient<APIConfig>();
services.AddTransient<FileService>();
services.AddTransient<AccountService>();
services.AddTransient<UserService>();
services.AddTransient<VideogameService>();
services.AddTransient<ExternalAuthorService>();
services.AddTransient<CommentService>();
services.AddTransient<NewsService>();
services.AddTransient<DiscordService>();
services.AddTransient<ResourceAccessService>();
services.AddTransient<ResourceCommunityService>();
services.AddTransient<ResourceVersionService>();
services.AddTransient<ResourceService>();
services.AddScoped<EmailService>();
services.AddScoped<DateTimeService>();
services.AddScoped<DBHelperService>();
services.AddScoped(typeof(GetFirstOrErrorQueryOperation<>));
services.AddScoped(typeof(GetSingleOrErrorQueryOperation<>));
services.AddScoped(typeof(GetByIdQueryOperation<,>));
services.AddScoped(typeof(GetByPropertyQueryOperation<,>));
services.AddScoped(typeof(GetLikeByNameQueryOperation<>));
services.AddScoped(typeof(GetLikeBySelectorQueryOperation<,>));
services.AddScoped(typeof(PaginatedListQueryOperation<,>));
services.AddHttpClient<CaptchaService>(client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
services.AddHttpClient("Discord", client =>
{
    client.BaseAddress = new Uri("https://discord.com/");
    client.Timeout = TimeSpan.FromSeconds(15);
});
services.AddHttpClient("DiscordBot", client =>
{
    client.Timeout = TimeSpan.FromSeconds(10);
});
services.AddCors(options =>
{
    options.AddDefaultPolicy(b =>
    {
        b.WithOrigins("*").AllowAnyMethod().AllowAnyHeader();
    });
});
services.AddDistributedMemoryCache();
services.AddSession();
services.AddControllersWithViews()
    .AddControllersAsServices();

services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
});
services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    (string Name, int Amount, int Minutes)[] limits =
    [
        ("login", 20, 1),
        ("register", 5, 10),
        ("password-email", 3, 15),
        ("reads", 240, 1),
        ("comments", 20, 1),
        ("votes", 60, 1),
        ("writes", 30, 1),
        ("uploads", 10, 10)
    ];

    foreach (var (Name, Amount, Minutes) in limits)
        options.AddPolicy(Name, context =>
        {
            string? userId = Name is "login" or "register" or "password-email" or "reads" ?
                null : context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            string key = userId != null ? $"user:{userId}" :
                $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

            return RateLimitPartition.GetSlidingWindowLimiter(key, _ =>
                new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = Amount,
                    Window = TimeSpan.FromMinutes(Minutes),
                    SegmentsPerWindow = 6,
                    QueueLimit = 0,
                    AutoReplenishment = true
                });
        });

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        context.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName == "uploads" ?
            RateLimitPartition.GetConcurrencyLimiter("uploads", _ =>
                new ConcurrencyLimiterOptions
                {
                    PermitLimit = 2,
                    QueueLimit = 0
                }) :
            RateLimitPartition.GetNoLimiter("other"));

    options.OnRejected = async (context, ct) =>
    {
        var response = context.HttpContext.Response;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
            response.Headers.RetryAfter = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
                .ToString(CultureInfo.InvariantCulture);

        // Native form navigation: show the existing Login view instead of raw JSON.
        if (context.HttpContext.Request.Path.Equals(new PathString("/api/login/submit")))
        {
            response.Redirect("/Login?result=rate-limited&returnUrl=" +
                Uri.EscapeDataString(context.HttpContext.Request.Query["returnUrl"].ToString()));
            return;
        }

        await response.WriteAsJsonAsync(new { error = "RateLimited" }, cancellationToken: ct);
    };
});

services.AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(
        builder.Configuration["DataProtection:KeyPath"]!))
    .SetApplicationName("SNEStorage");

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseCors();
app.UseSession();

app.Use(async (context, next) =>
{
    var token = context.Request.Cookies["__Host-SNEStorage.Auth"];

    if (!string.IsNullOrEmpty(token) &&
        !context.Request.Headers.ContainsKey("Authorization"))
    {
        context.Request.Headers.Authorization = "Bearer " + token;
        context.Items["BrowserJwt"] = true;
    }

    await next();
});

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.UseAntiforgery();

app.UseSwagger();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Api SNEStorage REST");
});

app.MapControllers();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
