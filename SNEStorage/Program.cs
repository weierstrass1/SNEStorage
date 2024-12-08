using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SNEStorage.Models;
using SNEStorage.Services;
using System.Text;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
IServiceCollection services = builder.Services;
// Add services to the container.
services.AddControllersWithViews();
services.AddDbContext<SnestorageContext>(options =>
{
    options.UseSqlServer(builder.Configuration.GetConnectionString("SNEStorageContext"));
});
services.AddRazorPages(opts =>
{
    opts.Conventions.AddPageRoute("/Home/Index", "");
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
services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", pol => pol.RequireClaim("Admin"));
    options.AddPolicy("Moderator", pol => pol.RequireClaim("Moderator"));
});
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
services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<SnestorageContext>()
    .AddDefaultTokenProviders();
services.AddHttpContextAccessor();
services.AddTransient<APIConfig>();
services.AddTransient<FileService>();
services.AddTransient<AccountService>();
services.AddCors(options =>
{
    options.AddDefaultPolicy(b =>
    {
        b.WithOrigins("*").AllowAnyMethod().AllowAnyHeader();
    });
});
services.AddSession();

var app = builder.Build();

app.UseSession();

//add token to request header.
app.Use(async (context, next) =>
{
    var token = context.Session.GetString("Token");
    if (!string.IsNullOrEmpty(token))
    {
        context.Request.Headers.Append("Authorization", "Bearer " + token);
    }
    await next();
});

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.UseSwagger();

app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Api SNEStorage REST");
});

app.UseCors();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
