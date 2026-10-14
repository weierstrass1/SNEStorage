using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SNEStorage.Controllers;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;
using StorageTimeZone = SNEStorage.Models.TimeZone;

namespace SNEStorageTesting;

public class RegisterControllerTests
{
    [Fact]
    public async Task GetRegistrationOptions_ReturnsSortedTimeZones_AndOnlyNonResourceVisibilities()
    {
        string databaseName = $"register-options-{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddMemoryCache();
        services.AddDbContext<SnestorageContext>(options =>
            options.UseInMemoryDatabase(databaseName));
        services.AddScoped<DBHelperService>();
        services.AddSingleton<CacheService>();
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<SnestorageContext>();
        context.TimeZones.AddRange(
            new StorageTimeZone { Id = 20, Name = "Later", Value = 20 },
            new StorageTimeZone { Id = 10, Name = "Earlier", Value = 10 });
        context.Visibilities.AddRange(
            new Visibility { Id = 2, Name = "Resource only", ResourceOnly = true },
            new Visibility { Id = 1, Name = "Public", ResourceOnly = false });
        await context.SaveChangesAsync();

        // The options action reads through the real cache, without using AccountService.
        var controller = new RegisterController(null!, provider.GetRequiredService<CacheService>());

        var action = await controller.GetRegistrationOptions(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(action.Result);
        var result = Assert.IsType<RegistrationOptionsDto>(ok.Value);
        Assert.Equal([10, 20], result.TimeZones.Select(timeZone => timeZone.Id));
        Assert.Equal(["Earlier", "Later"], result.TimeZones.Select(timeZone => timeZone.Name));
        var emailVisibility = Assert.Single(result.EmailVisibilities);
        Assert.Equal(1, emailVisibility.Id);
        Assert.Equal("Public", emailVisibility.Name);
    }
}
