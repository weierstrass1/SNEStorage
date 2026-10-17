using System.Reflection;
using System.Security.Claims;
using SNEStorage.Components.Pages;
using SNEStorage.Components.Resources;
using SNEStorage.Components.Shared;
using SNEStorage.DTOs;

namespace SNEStorageTesting;

public class ResourceStaffPresentationTests
{
    [Fact]
    public void OnlyEditPagesPermitTheStaffResourceException()
    {
        Assert.True(typeof(ResourceEdit).GetCustomAttribute<RequireAccountAttribute>()!.AllowStaffResourceEdit);
        Assert.True(typeof(ResourceVersionEdit).GetCustomAttribute<RequireAccountAttribute>()!.AllowStaffResourceEdit);
        Assert.False(typeof(ResourceVersionCreate).GetCustomAttribute<RequireAccountAttribute>()!.AllowStaffResourceEdit);
    }

    [Theory]
    [InlineData(false, false, "user", false)]
    [InlineData(false, true, "staff", true)]
    [InlineData(true, false, "author", true)]
    [InlineData(true, true, null, false)]
    [InlineData(false, true, "", false)]
    public void StaffEditAccessDoesNotChangeContributorAccess(bool contributor, bool staffEdit,
        string? userId, bool expected)
    {
        var resource = new ResourceReadDTO
        {
            Name = "Resource", Description = "Description", CanEdit = contributor,
            Videogame = new() { Id = 1, Name = "Game" },
            Visibility = new() { Id = 1, Name = "Public" }
        };
        var type = typeof(ResourceEditor).Assembly.GetType("SNEStorage.Components.Resources.ResourceManagementAccess")!;
        var method = type.GetMethod("CanDisplay", BindingFlags.Public | BindingFlags.Static)!;

        Assert.Equal(expected, (bool)method.Invoke(null, [resource, userId, staffEdit])!);
        Assert.Equal(contributor, resource.CanEdit);
    }

    [Theory]
    [InlineData("staff", "staff", true, true)]
    [InlineData("staff", "another-account", true, false)]
    [InlineData("user", "user", false, false)]
    public void StaffClaimMustMatchTheVerifiedAccount(string claimUserId, string profileUserId,
        bool moderator, bool expected)
    {
        List<Claim> claims = [new(ClaimTypes.NameIdentifier, claimUserId)];
        if (moderator) claims.Add(new("Moderator", "1"));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var type = typeof(RequireAccountAttribute).Assembly.GetType("SNEStorage.Components.Shared.StaffAccess")!;
        var method = type.GetMethod("IsAllowed", BindingFlags.Public | BindingFlags.Static)!;

        Assert.Equal(expected, (bool)method.Invoke(null, [principal, profileUserId])!);
    }
}
