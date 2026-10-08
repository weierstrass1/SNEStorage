using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SNEStorage.Controllers;
using SNEStorage.DTOs;
using SNEStorage.Models;
using StorageTimeZone = SNEStorage.Models.TimeZone;

namespace SNEStorageTesting;

public class RegisterControllerTests
{
    [Fact]
    public async Task GetRegistrationOptions_ReturnsSortedTimeZones_AndOnlyNonResourceVisibilities()
    {
        var options = new DbContextOptionsBuilder<SnestorageContext>()
            .UseInMemoryDatabase($"register-options-{Guid.NewGuid():N}")
            .Options;

        await using var context = new SnestorageContext(options);
        context.TimeZones.AddRange(
            new StorageTimeZone { Id = 20, Name = "Later", Value = 20 },
            new StorageTimeZone { Id = 10, Name = "Earlier", Value = 10 });
        context.Visibilities.AddRange(
            new Visibility { Id = 2, Name = "Resource only", ResourceOnly = true },
            new Visibility { Id = 1, Name = "Public", ResourceOnly = false });
        await context.SaveChangesAsync();

        // This action only uses the context; the account controller isn't involved in this request.
        var controller = new RegisterController(null!, context);

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
