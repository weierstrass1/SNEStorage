using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SNEStorage.Components;
using SNEStorage.Models;
using SNEStorage.Services;
using System.Text;
using System.Text.Json.Serialization;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
IServiceCollection services = builder.Services;
// Add services to the container.
services.AddControllers()
    .AddJsonOptions(options =>
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
    .AddJwtBearer(options => options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false,
        ValidateAudience = false,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8
            .GetBytes(builder.Configuration["JWTKey"]!)),
        ClockSkew = TimeSpan.Zero
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
        })
    .AddEntityFrameworkStores<SnestorageContext>()
    .AddDefaultTokenProviders();
services.AddHttpContextAccessor();
services.AddTransient<APIConfig>();
services.AddTransient<FileService>();
services.AddTransient<AccountService>();
services.AddTransient<ResourceService>();
services.AddTransient<UserService>();
services.AddTransient<VideogameService>();
services.AddTransient<ExternalAuthorService>();
services.AddTransient<CommentService>();
services.AddScoped<EmailService>();
services.AddScoped<DateTimeService>();
services.AddScoped<DBHelperService>();
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
