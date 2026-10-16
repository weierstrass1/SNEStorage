using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SNEStorage.Models;
using SNEStorage.Services;
using SNEStorage.Services.QueryComponents;

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

    public GetFirstOrDefaultQueryOperation<T> CreateGetFirstOrDefault<T>()
        where T : class => new(DBHelper);

    public GetFirstOrErrorQueryOperation<T> CreateGetFirstOrError<T>()
        where T : class => new(
            CreateGetFirstOrDefault<T>(),
            NullLogger<GetFirstOrErrorQueryOperation<T>>.Instance);

    public GetSingleOrErrorQueryOperation<T> CreateGetSingleOrError<T>()
        where T : class => new(
            DBHelper, NullLogger<GetSingleOrErrorQueryOperation<T>>.Instance);

    public GetByPropertyQueryOperation<T, TValue> CreateGetByProperty<T, TValue>()
        where T : class => new(CreateGetFirstOrError<T>());

    public GetListQueryOperation<T> CreateGetList<T>() => new(DBHelper);

    public GetLikeBySelectorQueryOperation<T, TResult>
        CreateGetLikeBySelector<T, TResult>() => new(
            DBHelper,
            NullLogger<GetLikeBySelectorQueryOperation<T, TResult>>.Instance);

    public PaginatedListQueryOperation<T, TSort> CreatePaginatedList<T, TSort>()
        where TSort : struct, Enum => new(
            CreateGetList<T>(),
            NullLogger<PaginatedListQueryOperation<T, TSort>>.Instance);

    public ValueTask DisposeAsync() => Context.DisposeAsync();
}
