using System.Data;
using System.Data.Common;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;
using SNEStorage.Services.QueryComponents;

namespace SNEStorageTesting;

// Opt-in integration tests. Never read application configuration or production secrets.
public sealed class LocalSqlFactAttribute : FactAttribute
{
    public LocalSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SNESTORAGE_TEST_LOCALDB")))
            Skip = "Set SNESTORAGE_TEST_LOCALDB to a LocalDB instance to run isolated SQL Server tests.";
    }
}

public sealed class LocalSqlTheoryAttribute : TheoryAttribute
{
    public LocalSqlTheoryAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SNESTORAGE_TEST_LOCALDB")))
            Skip = "Set SNESTORAGE_TEST_LOCALDB to a LocalDB instance to run isolated SQL Server tests.";
    }
}

public sealed class ResourcePersistenceSqlServerTests(SqlResourceDatabase database)
    : IClassFixture<SqlResourceDatabase>
{
    [LocalSqlTheory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Save_AcceptsPublicAndUnlistedDependencies_AndCompletesBatch(int visibility)
    {
        long target = await database.SeedResource(visibility);
        await using var context = database.CreateContext();
        var source = database.NewResource();
        source.ResourceDependencies.Add(Link(target));
        context.Resources.Add(source);
        var batch = await database.CreateBatch();

        var result = await Save(context, source, batch.Manifest);

        Assert.True(result.IsT0);
        Assert.False(System.IO.File.Exists(batch.Manifest));
        Assert.True(System.IO.File.Exists(batch.Upload));
        await using var check = database.CreateContext();
        Assert.True(await check.ResourceDependencies.AnyAsync(link =>
            link.ResourceId == source.Id && link.DependencyResourceId == target));
    }

    [LocalSqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_RejectsPrivateDependencies_ForCreateAndEdit(bool existingSource)
    {
        long target = await database.SeedResource(3);
        long sourceId = existingSource ? await database.SeedResource() : 0;
        await using var context = database.CreateContext();
        var source = existingSource ? await context.Resources.SingleAsync(r => r.Id == sourceId) : database.NewResource();
        if (!existingSource) context.Resources.Add(source);
        source.ResourceDependencies.Add(Link(target));
        var batch = await database.CreateBatch();

        var result = await Save(context, source, batch.Manifest);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.ResourceNotFound, result.AsT1.Result);
        Assert.False(System.IO.File.Exists(batch.Manifest));
        Assert.False(System.IO.File.Exists(batch.Upload));
        Assert.Empty(context.ChangeTracker.Entries());
        await using var check = database.CreateContext();
        Assert.False(await check.ResourceDependencies.AnyAsync(link =>
            link.ResourceId == source.Id && link.DependencyResourceId == target));
    }

    [LocalSqlFact]
    public async Task Save_RejectsPrivacy_WhenAnotherResourceAlreadyDependsOnIt()
    {
        long target = await database.SeedResource();
        long source = await database.SeedResource();
        await using (var setup = database.CreateContext())
        {
            setup.ResourceDependencies.Add(new() { Id = 0, ResourceId = source, DependencyResourceId = target });
            await setup.SaveChangesAsync();
        }
        await using var context = database.CreateContext();
        var resource = await context.Resources.SingleAsync(r => r.Id == target);
        resource.VisibilityId = 3;
        var batch = await database.CreateBatch();

        var result = await Save(context, resource, batch.Manifest);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.ResourceHasDependents, result.AsT1.Result);
        await using var check = database.CreateContext();
        Assert.Equal(1, await check.Resources.Where(r => r.Id == target).Select(r => r.VisibilityId).SingleAsync());
        Assert.False(System.IO.File.Exists(batch.Upload));
    }

    [LocalSqlTheory]
    [InlineData(false, 1, false)]
    [InlineData(true, 1, true)]
    [InlineData(true, 2, false)]
    [InlineData(true, 3, false)]
    public async Task Save_RechecksSealFromDatabase_AndRequiresPublicVisibility(bool staff, int visibility, bool allowed)
    {
        long id = await database.SeedResource();
        await using var context = database.CreateContext();
        var resource = await context.Resources.Include(r => r.ResourceVersions).SingleAsync(r => r.Id == id);
        Assert.DoesNotContain(resource.ResourceVersions, version => version.SealOfQuality);
        // The resource was awarded a seal after the request loaded its entities.
        await using (var other = database.CreateContext())
            await other.ResourceVersions.Where(v => v.ResourceId == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(v => v.SealOfQuality, true));
        resource.Description = "Changed description after the initial request read.";
        resource.VisibilityId = visibility;
        var batch = await database.CreateBatch();

        var result = await Save(context, resource, batch.Manifest, staff: staff);

        Assert.Equal(allowed, result.IsT0);
        if (!allowed) Assert.Same(ServiceResult.ResourceHasSealOfQuality, result.AsT1.Result);
        await using var check = database.CreateContext();
        Assert.Equal(allowed ? resource.Description : SqlResourceDatabase.OriginalDescription,
            await check.Resources.Where(r => r.Id == id).Select(r => r.Description).SingleAsync());
        Assert.Equal(allowed, System.IO.File.Exists(batch.Upload));
    }

    [LocalSqlFact]
    public async Task Save_UsesCurrentVisibility_WhenTheTrackedVisibilityWasNotModified()
    {
        long id = await database.SeedResource();
        await using var context = database.CreateContext();
        var resource = await context.Resources.SingleAsync(r => r.Id == id);
        await using (var other = database.CreateContext())
            await other.Resources.Where(r => r.Id == id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(r => r.VisibilityId, 2));
        resource.Description = "Only the description was changed by this request.";
        var batch = await database.CreateBatch();

        var result = await Save(context, resource, batch.Manifest);

        Assert.True(result.IsT0);
        await using var check = database.CreateContext();
        Assert.Equal(2, await check.Resources.Where(r => r.Id == id).Select(r => r.VisibilityId).SingleAsync());
    }

    [LocalSqlFact]
    public async Task Save_RollsBackExecutedSql_AndDeletesNewFiles_WhenSaveFails()
    {
        long id = await database.SeedResource();
        await using var context = database.CreateContext(new ThrowAfterSave());
        var resource = await context.Resources.SingleAsync(r => r.Id == id);
        resource.Description = "This SQL update must be rolled back.";
        var batch = await database.CreateBatch();

        var result = await Save(context, resource, batch.Manifest);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.DBError, result.AsT1.Result);
        Assert.Empty(context.ChangeTracker.Entries());
        Assert.False(System.IO.File.Exists(batch.Manifest));
        Assert.False(System.IO.File.Exists(batch.Upload));
        await using var check = database.CreateContext();
        Assert.Equal(SqlResourceDatabase.OriginalDescription,
            await check.Resources.Where(r => r.Id == id).Select(r => r.Description).SingleAsync());
    }

    [LocalSqlFact]
    public async Task Save_KeepsFilesAndManifest_WhenCommitSucceededButItsResponseFailed()
    {
        long id = await database.SeedResource();
        await using var context = database.CreateContext(new ThrowAfterCommit());
        var resource = await context.Resources.SingleAsync(r => r.Id == id);
        resource.Description = "This update was committed before the simulated failure.";
        var batch = await database.CreateBatch();

        var result = await Save(context, resource, batch.Manifest);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.DBError, result.AsT1.Result);
        Assert.True(System.IO.File.Exists(batch.Manifest));
        Assert.True(System.IO.File.Exists(batch.Upload));
        await using var check = database.CreateContext();
        Assert.Equal(resource.Description,
            await check.Resources.Where(r => r.Id == id).Select(r => r.Description).SingleAsync());
    }

    [LocalSqlFact]
    public async Task Save_CleansBatch_EvenWhenTheClientTokenIsAlreadyCancelled()
    {
        await using var context = database.CreateContext();
        var resource = database.NewResource();
        context.Resources.Add(resource);
        var batch = await database.CreateBatch();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await Save(context, resource, batch.Manifest, cancellation.Token);

        Assert.True(result.IsT1);
        Assert.Same(ServiceResult.RequestCancelled, result.AsT1.Result);
        Assert.False(System.IO.File.Exists(batch.Manifest));
        Assert.False(System.IO.File.Exists(batch.Upload));
    }

    [LocalSqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_ConcurrentPrivacyAndDependency_CannotBothCommit(bool existingSource)
    {
        long target = await database.SeedResource();
        long sourceId = existingSource ? await database.SeedResource() : 0;
        var privacyGate = new PauseBeforeSave();
        var dependencyGate = new PauseBeforeSave();
        await using var privacyContext = database.CreateContext(privacyGate);
        await using var dependencyContext = database.CreateContext(dependencyGate);
        var targetResource = await privacyContext.Resources.SingleAsync(r => r.Id == target);
        var sourceResource = existingSource ? await dependencyContext.Resources
            .Include(r => r.ResourceDependencies).SingleAsync(r => r.Id == sourceId) : database.NewResource();
        if (!existingSource) dependencyContext.Resources.Add(sourceResource);
        targetResource.VisibilityId = 3;
        sourceResource.ResourceDependencies.Add(Link(target));
        var privacyBatch = await database.CreateBatch();
        var dependencyBatch = await database.CreateBatch();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(40));

        // Pause each real service call after its validation, while SQL locks remain held.
        var privacyTask = Save(privacyContext, targetResource, privacyBatch.Manifest, timeout.Token);
        await privacyGate.Ready.Task.WaitAsync(timeout.Token);
        var dependencyTask = Save(dependencyContext, sourceResource, dependencyBatch.Manifest, timeout.Token);
        try
        {
            await dependencyGate.Ready.Task.WaitAsync(timeout.Token);
        }
        finally
        {
            privacyGate.Release.TrySetResult();
            dependencyGate.Release.TrySetResult();
        }
        var results = await Task.WhenAll(privacyTask, dependencyTask).WaitAsync(timeout.Token);

        Assert.Single(results, result => result.IsT0);
        await using var check = database.CreateContext();
        bool isPrivate = await check.Resources.AnyAsync(r => r.Id == target && r.VisibilityId == 3);
        bool hasDependency = await check.ResourceDependencies.AnyAsync(link =>
            link.ResourceId == sourceResource.Id && link.DependencyResourceId == target);
        Assert.False(isPrivate && hasDependency);
        for (int index = 0; index < results.Length; index++)
        {
            var batch = index == 0 ? privacyBatch : dependencyBatch;
            Assert.Equal(results[index].IsT0, System.IO.File.Exists(batch.Upload));
            Assert.False(System.IO.File.Exists(batch.Manifest));
        }
    }

    private static ResourceDependency Link(long target) => new()
    { Id = 0, ResourceId = 0, DependencyResourceId = target };

    private Task<OneOf<Resource, ServiceResultContext>> Save(SnestorageContext context,
        Resource resource, string batch, CancellationToken ct = default, bool staff = false)
    {
        var service = database.CreateService(context, staff);
        var method = typeof(ResourceService).GetMethod("SaveResourceChanges", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task<OneOf<Resource, ServiceResultContext>>)method.Invoke(service, [resource, batch, ct])!;
    }

    private sealed class PauseBeforeSave : SaveChangesInterceptor
    {
        public TaskCompletionSource Ready { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Assert.Equal(IsolationLevel.Serializable,
                eventData.Context!.Database.CurrentTransaction!.GetDbTransaction().IsolationLevel);
            Ready.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private sealed class ThrowAfterSave : SaveChangesInterceptor
    {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("Injected failure after SQL writes, before commit.");
    }

    private sealed class ThrowAfterCommit : DbTransactionInterceptor
    {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) =>
            throw new IOException("Injected lost commit acknowledgement.");
    }
}

public sealed class SqlResourceDatabase : IAsyncLifetime
{
    public const string OriginalDescription = "Original resource description for isolated SQL tests.";
    private string _connectionString = "";
    private string _masterConnection = "";
    private string _databaseName = "";
    private string _directory = "";
    private long _gameId;
    private long _fileTypeId;

    public async Task InitializeAsync()
    {
        var instance = Environment.GetEnvironmentVariable("SNESTORAGE_TEST_LOCALDB");
        if (string.IsNullOrWhiteSpace(instance)) return;
        if (!instance.StartsWith("(localdb)\\", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("These tests only accept a local LocalDB instance.");
        string root = Environment.GetEnvironmentVariable("SNESTORAGE_TEST_DATA_DIRECTORY") ?? Path.GetTempPath();
        _databaseName = $"SNEStorageReleaseTests_{Guid.NewGuid():N}";
        _directory = Path.Combine(root, _databaseName);
        Directory.CreateDirectory(_directory);
        _masterConnection = new SqlConnectionStringBuilder
        { DataSource = instance, InitialCatalog = "master", IntegratedSecurity = true,
            TrustServerCertificate = true, ConnectTimeout = 10, Pooling = false }.ConnectionString;
        await using var connection = new SqlConnection(_masterConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"CREATE DATABASE [{_databaseName}] ON PRIMARY (NAME=N'{_databaseName}', FILENAME=N'{SqlLiteral(Path.Combine(_directory, "test.mdf"))}') LOG ON (NAME=N'{_databaseName}_log', FILENAME=N'{SqlLiteral(Path.Combine(_directory, "test.ldf"))}')";
        await command.ExecuteNonQueryAsync();
        _connectionString = new SqlConnectionStringBuilder(_masterConnection)
        { InitialCatalog = _databaseName }.ConnectionString;
        await using var context = CreateContext();
        // Use the production DDL for the tables exercised here, including their FKs
        // and indexes. Unrelated legacy tables are not required by this fixture.
        HashSet<string> tables = ["AspNetUsers", "file_type", "file", "videogame",
            "visibility", "score", "resource", "resource_version", "resource_dependency"];
        foreach (string batch in Regex.Split(context.Database.GenerateCreateScript(), @"^\s*GO\s*$", RegexOptions.Multiline))
        {
            var table = Regex.Match(batch, @"CREATE TABLE \[([^\]]+)\]");
            var index = Regex.Match(batch, @"CREATE (?:UNIQUE )?INDEX .*? ON \[([^\]]+)\]");
            if (table.Success && tables.Contains(table.Groups[1].Value) ||
                index.Success && tables.Contains(index.Groups[1].Value))
                await context.Database.ExecuteSqlRawAsync(batch);
        }
        await context.Database.ExecuteSqlRawAsync("""
            SET IDENTITY_INSERT [visibility] ON;
            INSERT INTO [visibility] ([id], [name], [resource_only]) VALUES
                (1, N'Public', 1), (2, N'Unlisted', 1), (3, N'Private', 1);
            SET IDENTITY_INSERT [visibility] OFF;
            """);
        context.Users.Add(new ApplicationUser { Id = "sql-test-owner", UserName = "sql-test-owner" });
        var game = new Videogame { Id = 0, Name = "Test Game", ReleaseDate = null };
        var fileType = new FileType { Name = "ZIP", Value = "application/zip" };
        context.AddRange(game, fileType);
        await context.SaveChangesAsync();
        _gameId = game.Id;
        _fileTypeId = fileType.Id;
    }

    public SnestorageContext CreateContext(params IInterceptor[] interceptors) =>
        new(new DbContextOptionsBuilder<SnestorageContext>()
            .UseSqlServer(_connectionString, options => options.CommandTimeout(15))
            .AddInterceptors(interceptors).Options);

    public Resource NewResource(int visibility = 1) => new()
    {
        Id = 0, Name = $"Test {Guid.NewGuid():N}", Description = OriginalDescription,
        VideogameId = _gameId, VisibilityId = visibility, ScoreId = 0, Score = new(),
        SubmitterId = "sql-test-owner", CreatedAt = DateTime.UtcNow,
        ResourceVersions = [new()
        {
            Id = 0, ResourceId = 0, FileId = 0, Version = "1.0", CreatedAt = DateTime.UtcNow,
            SubmitterUserId = "sql-test-owner",
            File = new() { URL = $"Storage/Resources/test/{Guid.NewGuid():N}.zip", FileTypeId = _fileTypeId }
        }]
    };

    public async Task<long> SeedResource(int visibility = 1)
    {
        await using var context = CreateContext();
        var resource = NewResource(visibility);
        context.Resources.Add(resource);
        await context.SaveChangesAsync();
        return resource.Id;
    }

    public async Task<(string Manifest, string Upload)> CreateBatch()
    {
        string upload = Path.Combine(_directory, $"{Guid.NewGuid():N}.upload");
        string manifest = Path.Combine(_directory, $"{Guid.NewGuid():N}.json");
        await System.IO.File.WriteAllTextAsync(upload, "Isolated test upload");
        await System.IO.File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(new[] { upload }));
        return (manifest, upload);
    }

    public ResourceService CreateService(SnestorageContext context, bool staff)
    {
        var helper = new DBHelperService(context, NullLogger<DBHelperService>.Instance);
        var accessor = new HttpContextAccessor { HttpContext = new DefaultHttpContext() };
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, "sql-test-owner")];
        if (staff) claims.Add(new("Moderator", "1"));
        accessor.HttpContext.User = new(new ClaimsIdentity(claims, "SqlTest"));
        var account = new AccountService(null!, null!, null!, null!, context, null!, accessor,
            null!, helper, null!, NullLogger<AccountService>.Instance);
        var files = new FileService(context, null!, helper, new SqlTestEnvironment(_directory),
            NullLogger<FileService>.Instance);
        return new(context, null!, files, account, helper, null!, null!, null!, null!, null!, null!, null!,
            NullLogger<ResourceService>.Instance);
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrEmpty(_databaseName)) return;
        if (!_databaseName.StartsWith("SNEStorageReleaseTests_", StringComparison.Ordinal) ||
            !Guid.TryParseExact(_databaseName["SNEStorageReleaseTests_".Length..], "N", out _))
            throw new InvalidOperationException("Refusing to drop an unexpected database.");
        await using var connection = new SqlConnection(_masterConnection);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{_databaseName}') IS NOT NULL BEGIN ALTER DATABASE [{_databaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_databaseName}]; END";
        await command.ExecuteNonQueryAsync();
        // Delete only individual test-created files in this fixture's unique directory.
        foreach (string path in Directory.EnumerateFiles(_directory)) System.IO.File.Delete(path);
        Directory.Delete(_directory, recursive: false);
    }

    private static string SqlLiteral(string value) => value.Replace("'", "''");

    private sealed class SqlTestEnvironment(string root) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "SNEStorageTesting";
        public string EnvironmentName { get; set; } = "Testing";
        public string ContentRootPath { get; set; } = root;
        public string WebRootPath { get; set; } = root;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }
}
