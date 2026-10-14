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

    public ValueTask DisposeAsync() => Context.DisposeAsync();
}
