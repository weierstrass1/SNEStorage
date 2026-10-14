using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using SNEStorage.DTOs;

namespace SNEStorageTesting;

public class DtoValidationTests
{
    [Theory(DisplayName = "Resource version validation accepts every supported version shape")]
    [InlineData("1")]
    [InlineData("1.2")]
    [InlineData("1.2.3")]
    [InlineData("1-alpha")]
    [InlineData("1.2-beta.10")]
    [InlineData("9999.9999.9999-rc.9999")]
    public void ResourceVersionCreate_AcceptsSupportedVersions(string version)
    {
        var dto = CreateValidVersion(version);

        Assert.Empty(Validate(dto));
    }

    [Theory(DisplayName = "Resource version validation rejects malformed or oversized versions")]
    [InlineData("")]
    [InlineData("1.2.3.4")]
    [InlineData("1.2-preview")]
    [InlineData("1-beta0")]
    [InlineData("10000")]
    [InlineData("1.2.3-beta.10000")]
    public void ResourceVersionCreate_RejectsUnsupportedVersions(string version)
    {
        var dto = CreateValidVersion(version);

        Assert.Contains(Validate(dto), result =>
            result.MemberNames.Contains(nameof(ResourceVersionCreateDTO.Version)));
    }

    [Theory(DisplayName = "Resource names require two to one hundred letters, numbers, or internal spaces")]
    [InlineData("A", false)]
    [InlineData("Valid Resource 2", true)]
    [InlineData(" Leading", false)]
    [InlineData("Trailing ", false)]
    [InlineData("Invalid-Name", false)]
    public void ResourceCreate_ValidatesNameFormat(string name, bool isValid)
    {
        var dto = CreateValidResource();
        dto.Name = name;

        bool hasNameError = Validate(dto).Any(result =>
            result.MemberNames.Contains(nameof(ResourceCreateDTO.Name)));

        Assert.Equal(isValid, !hasNameError);
    }

    [Theory(DisplayName = "Resource repository URLs accept only bounded HTTP and HTTPS addresses")]
    [InlineData(null, true)]
    [InlineData("https://github.com/owner/project", true)]
    [InlineData("http://example.com/resource", true)]
    [InlineData("ftp://example.com/resource", false)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("https://example.com/path with spaces", false)]
    public void ResourceCreate_ValidatesRepositoryUrl(string? url, bool isValid)
    {
        var dto = CreateValidResource();
        dto.RepositoryURL = url;

        bool hasUrlError = Validate(dto).Any(result =>
            result.MemberNames.Contains(nameof(ResourceCreateDTO.RepositoryURL)));

        Assert.Equal(isValid, !hasUrlError);
    }

    [Fact(DisplayName = "Resource creation requires at least one resource type")]
    public void ResourceCreate_RejectsEmptyTypeSelection()
    {
        var dto = CreateValidResource();
        dto.Types = [];

        Assert.Contains(Validate(dto), result =>
            result.MemberNames.Contains(nameof(ResourceCreateDTO.Types)));
    }

    [Fact(DisplayName = "Resource creation limits tags to fifty entries")]
    public void ResourceCreate_RejectsMoreThanFiftyTags()
    {
        var dto = CreateValidResource();
        dto.Tags = Enumerable.Range(1, 51).Select(index => $"tag-{index}").ToArray();

        Assert.Contains(Validate(dto), result =>
            result.MemberNames.Contains(nameof(ResourceCreateDTO.Tags)));
    }

    [Theory(DisplayName = "Resource score filters accept only values from one through five")]
    [InlineData(null, true)]
    [InlineData(1, true)]
    [InlineData(5, true)]
    [InlineData(0, false)]
    [InlineData(6, false)]
    public void ResourceFinder_ValidatesScoreBounds(int? score, bool isValid)
    {
        var dto = new ResourceFinderDTO { ScoreLowerLimit = score };

        bool hasScoreError = Validate(dto).Any(result =>
            result.MemberNames.Contains(nameof(ResourceFinderDTO.ScoreLowerLimit)));

        Assert.Equal(isValid, !hasScoreError);
    }

    [Fact(DisplayName = "Resource sorting rejects undefined enum values")]
    public void ResourceSorting_RejectsUndefinedValues()
    {
        var dto = new ResourceSortingDTO { SortType = (ResourceSorting)int.MaxValue };

        Assert.Contains(Validate(dto), result =>
            result.MemberNames.Contains(nameof(ResourceSortingDTO.SortType)));
    }

    [Theory(DisplayName = "Profile colors accept null or a six-digit hexadecimal color")]
    [InlineData(null, true)]
    [InlineData("#12aBcF", true)]
    [InlineData("12ABCF", false)]
    [InlineData("#12345", false)]
    [InlineData("#12345G", false)]
    public void EditProfile_ValidatesUsernameColor(string? color, bool isValid)
    {
        var dto = new EditProfileDTO { UsernameColor = color };

        bool hasColorError = Validate(dto).Any(result =>
            result.MemberNames.Contains(nameof(EditProfileDTO.UsernameColor)));

        Assert.Equal(isValid, !hasColorError);
    }

    [Fact(DisplayName = "Profile social fields preserve empty removal values but reject oversized text")]
    public void EditProfile_ValidatesSocialFieldLength()
    {
        Assert.Empty(Validate(new EditProfileDTO { Patreon = "" }));

        var oversized = new EditProfileDTO { Patreon = new string('a', 101) };
        Assert.Contains(Validate(oversized), result =>
            result.MemberNames.Contains(nameof(EditProfileDTO.Patreon)));
    }

    [Fact(DisplayName = "Login usernames cannot exceed the Identity username limit")]
    public void LoginCredentials_RejectsOversizedUsername()
    {
        LoginCredentials credentials = new()
        {
            User = new string('a', 257),
            Password = "Valid1!Password"
        };

        Assert.Contains(Validate(credentials), result =>
            result.MemberNames.Contains(nameof(LoginCredentials.User)));
    }

    [Fact(DisplayName = "External dependencies enforce bounded names and URLs")]
    public void ExternalDependency_RejectsOversizedFields()
    {
        ExternalDependencyDTO dto = new()
        {
            DependencyName = new string('a', 101),
            DependencyURL = new string('b', 256),
            Mirror1 = new string('c', 256)
        };

        var invalidMembers = Validate(dto)
            .SelectMany(result => result.MemberNames)
            .ToHashSet();

        Assert.Contains(nameof(ExternalDependencyDTO.DependencyName), invalidMembers);
        Assert.Contains(nameof(ExternalDependencyDTO.DependencyURL), invalidMembers);
        Assert.Contains(nameof(ExternalDependencyDTO.Mirror1), invalidMembers);
    }

    private static ResourceCreateDTO CreateValidResource()
    {
        return new ResourceCreateDTO
        {
            Id = 0,
            Name = "Valid Resource",
            VideogameId = 1,
            Description = "A valid resource description.",
            ChangeLog = "Initial release notes.",
            Version = "1.0.0",
            File = CreateFile(),
            Types = [1]
        };
    }

    private static ResourceVersionCreateDTO CreateValidVersion(string version)
    {
        return new ResourceVersionCreateDTO
        {
            Version = version,
            ReleaseNotes = "Valid release notes.",
            File = CreateFile()
        };
    }

    private static IFormFile CreateFile()
    {
        return new FormFile(new MemoryStream([1]), 0, 1, "file", "resource.zip")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/zip"
        };
    }

    private static List<ValidationResult> Validate(object instance)
    {
        List<ValidationResult> results = [];
        Validator.TryValidateObject(instance, new ValidationContext(instance),
            results, validateAllProperties: true);
        return results;
    }
}
