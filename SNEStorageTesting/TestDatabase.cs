using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorageTesting;

internal sealed class TestDatabase : IAsyncDisposable
{
    public SnestorageContext Context { get; }
    public DBHelperService DBHelper { get; }

    public TestDatabase()
    {
        var options = new DbContextOptionsBuilder<SnestorageContext>()
            .UseInMemoryDatabase($"snestorage-tests-{Guid.NewGuid():N}")
            .Options;

        Context = new SnestorageContext(options);
        DBHelper = new DBHelperService(Context, NullLogger<DBHelperService>.Instance);
    }

    public AccountService CreateAccountService(string? userId = null,
        bool isModerator = false, bool isAdmin = false)
    {
        List<Claim> claims = [];
        if (!string.IsNullOrWhiteSpace(userId))
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));
        if (isModerator)
            claims.Add(new Claim("Moderator", "1"));
        if (isAdmin)
            claims.Add(new Claim("Admin", "1"));

        ClaimsIdentity identity = new(claims,
            string.IsNullOrWhiteSpace(userId) ? null : "Test");
        IHttpContextAccessor accessor = new HttpContextAccessor
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        return new AccountService(
            null!, null!, null!, null!, Context, null!, accessor, null!, DBHelper,
            null!, NullLogger<AccountService>.Instance);
    }

    public ValueTask DisposeAsync() => Context.DisposeAsync();
}
