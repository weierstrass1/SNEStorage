using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using OneOf;
using SNEStorage.DTOs;

namespace SNEStorageTesting;

public class ServiceResultExtensionsTests
{
    public static TheoryData<ServiceResult, Type, int?, ServiceResultTypes> ErrorMappings => new()
    {
        { ServiceResult.Invalid, typeof(BadRequestObjectResult), 400, ServiceResultTypes.Invalid },
        { ServiceResult.Unauthorized, typeof(UnauthorizedObjectResult), 401, ServiceResultTypes.Unauthorized },
        { ServiceResult.Forbidden, typeof(ObjectResult), 403, ServiceResultTypes.Unauthorized },
        { ServiceResult.ResourceNotFound, typeof(NotFoundObjectResult), 404, ServiceResultTypes.ResourceNotFound },
        { ServiceResult.VersionAlreadyExists, typeof(ConflictObjectResult), 409, ServiceResultTypes.VersionAlreadyExists },
        { ServiceResult.FileSizeSurpassTheLimit, typeof(ObjectResult), 413, ServiceResultTypes.FileSizeSurpassTheLimit },
        { ServiceResult.InvalidMime, typeof(ObjectResult), 415, ServiceResultTypes.InvalidMime },
        { ServiceResult.RequestCancelled, typeof(ObjectResult), 499, ServiceResultTypes.CancellationTokenError },
        { ServiceResult.OAuthTokenExchangeError, typeof(ObjectResult), 502, ServiceResultTypes.OAuthTokenExchangeError },
        { ServiceResult.Unavailable, typeof(ObjectResult), 503, ServiceResultTypes.Unavailable },
        { ServiceResult.DBError, typeof(ObjectResult), 500, ServiceResultTypes.DBError },
        { ServiceResult.UnsupportedEvent, typeof(ObjectResult), 500, ServiceResultTypes.UnsupportedEvent }
    };

    [Theory(DisplayName = "ServiceResult errors map to the documented HTTP status and response code")]
    [MemberData(nameof(ErrorMappings))]
    public void GetActionResult_MapsErrorToExpectedHttpResponse(ServiceResult serviceResult,
        Type expectedType, int? expectedStatus, ServiceResultTypes expectedCode)
    {
        var controller = new TestController();
        var context = serviceResult.Return(NullLogger<ServiceResultExtensionsTests>.Instance);

        var result = controller.GetActionResult(context);

        Assert.IsType(expectedType, result);
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatus, objectResult.StatusCode);
        Assert.Equal($"TestController-{expectedCode}", objectResult.Value);
    }

    [Fact(DisplayName = "A successful result returns the supplied response body")]
    public void GetActionResult_ReturnsOk_WithExplicitValue()
    {
        var controller = new TestController();
        var context = ServiceResult.Success.Return(
            NullLogger<ServiceResultExtensionsTests>.Instance);

        var result = controller.GetActionResult(context, new TestResponse(12));

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(12, Assert.IsType<TestResponse>(ok.Value).Id);
    }

    [Fact(DisplayName = "A no-content result returns HTTP 204 without a body")]
    public void GetActionResult_ReturnsNoContent_ForNoContentResult()
    {
        var controller = new TestController();
        var context = ServiceResult.NoContent.Return(
            NullLogger<ServiceResultExtensionsTests>.Instance);

        var result = controller.GetActionResult(context);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact(DisplayName = "A physical-file result preserves path, MIME type, name, and range support")]
    public void GetActionResult_ReturnsPhysicalFile_WithAllFileMetadata()
    {
        var controller = new TestController();
        var context = ServiceResult.PhysicalFile.Return(
            NullLogger<ServiceResultExtensionsTests>.Instance);
        PhysicalFileData file = new("C:\\storage\\resource.zip", "application/zip",
            "resource.zip", false);

        var result = controller.GetActionResult(context, file);

        var physicalFile = Assert.IsType<PhysicalFileResult>(result);
        Assert.Equal(file.Path, physicalFile.FileName);
        Assert.Equal(file.ContentType, physicalFile.ContentType);
        Assert.Equal(file.DownloadName, physicalFile.FileDownloadName);
        Assert.Equal(file.EnableRangeProcessing, physicalFile.EnableRangeProcessing);
    }

    [Fact(DisplayName = "A redirect result returns the supplied destination URL")]
    public void GetActionResult_ReturnsRedirect_WithDestination()
    {
        var controller = new TestController();
        var context = ServiceResult.Redirect.Return(
            NullLogger<ServiceResultExtensionsTests>.Instance);

        var result = controller.GetActionResult(context, "/destination");

        var redirect = Assert.IsType<RedirectResult>(result);
        Assert.Equal("/destination", redirect.Url);
    }

    [Fact(DisplayName = "A typed successful result is returned as HTTP 200")]
    public void GetActionResult_ReturnsOk_ForTypedSuccess()
    {
        var controller = new TestController();
        OneOf<string, ServiceResultContext> serviceResult = "payload";

        ActionResult<string> result = controller.GetActionResult(serviceResult);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Equal("payload", ok.Value);
    }

    [Fact(DisplayName = "A typed error uses the same mapping and controller prefix")]
    public void GetActionResult_MapsError_ForTypedResult()
    {
        var controller = new TestController();
        OneOf<string, ServiceResultContext> serviceResult = ServiceResult.UserNotFound.Return(
            NullLogger<ServiceResultExtensionsTests>.Instance);

        ActionResult<string> result = controller.GetActionResult(serviceResult);

        var notFound = Assert.IsType<NotFoundObjectResult>(result.Result);
        Assert.Equal("TestController-UserNotFound", notFound.Value);
    }

    private sealed record TestResponse(int Id);
    private sealed class TestController : ControllerBase;
}
