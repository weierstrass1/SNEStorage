using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SNEStorage.Components;
using SNEStorage.Discord;
using SNEStorage.Models;
using SNEStorage.Services;
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
            !string.IsNullOrWhiteSpace(options.ApplicationId) &&
            !string.IsNullOrWhiteSpace(options.GuildId) &&
            !string.IsNullOrWhiteSpace(options.ClientSecret) &&
            !string.IsNullOrWhiteSpace(options.BotToken)
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
    options.UseSqlServer(builder.Configuration.GetConnectionString("SNEStorageContext"));
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
                var accountService = context.HttpContext.RequestServices
                    .GetRequiredService<AccountService>();

                var userId = context.Principal?
                    .FindFirstValue(ClaimTypes.NameIdentifier);
                var stamp = context.Principal?
                    .FindFirstValue("security_stamp");

                if (!await accountService.ValidateJwt(userId, stamp,
                    context.HttpContext.RequestAborted))
                    context.Fail("Invalid or revoked token.");
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
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
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
services.AddHttpContextAccessor();
services.AddMemoryCache();
services.AddSingleton<CacheService>();
services.AddSingleton<CaptchaFrequencyService>();
services.AddTransient<APIConfig>();
services.AddTransient<FileService>();
services.AddTransient<AccountService>();
services.AddTransient<ResourceService>();
services.AddTransient<UserService>();
services.AddTransient<VideogameService>();
services.AddTransient<ExternalAuthorService>();
services.AddTransient<CommentService>();
services.AddTransient<NewsService>();
services.AddScoped<EmailService>();
services.AddScoped<DateTimeService>();
services.AddScoped<DBHelperService>();
services.AddHttpClient<CaptchaService>(client =>
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

    foreach (var limit in limits)
        options.AddPolicy(limit.Name, context =>
        {
            string? userId = limit.Name is "login" or "register" or "password-email" or "reads" ?
                null : context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            string key = userId != null ? $"user:{userId}" :
                $"ip:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";

            return RateLimitPartition.GetSlidingWindowLimiter(key, _ =>
                new SlidingWindowRateLimiterOptions
                {
                    PermitLimit = limit.Amount,
                    Window = TimeSpan.FromMinutes(limit.Minutes),
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
            response.Headers["Retry-After"] = Math.Max(1, (int)Math.Ceiling(retryAfter.TotalSeconds))
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

// Add the session JWT to requests before authentication runs.
app.Use(async (context, next) =>
{
    var token = context.Session.GetString("Token");
    if (!string.IsNullOrEmpty(token))
    {
        context.Request.Headers.Append("Authorization", "Bearer " + token);
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
