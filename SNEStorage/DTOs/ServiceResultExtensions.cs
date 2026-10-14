using Microsoft.AspNetCore.Mvc;
using OneOf;

namespace SNEStorage.DTOs;

public static class ServiceResultExtensions
{
    public static ActionResult GetActionResult(this ControllerBase controller,
        ServiceResultContext context, object? value = null)
    {
        ServiceResult result = context.Result;

        string response =
            $"{controller.GetType().Name}-{result.Type}";

        return result.ActionType switch
        {
            ActionResultType.Ok =>
                controller.Ok(value ?? response),

            ActionResultType.NoContent =>
                controller.NoContent(),

            ActionResultType.PhysicalFile
                when value is PhysicalFileData file =>
                    controller.PhysicalFile(
                        file.Path,
                        file.ContentType,
                        file.DownloadName,
                        file.EnableRangeProcessing),

            ActionResultType.Redirect
                when value is string url =>
                    controller.Redirect(url),

            ActionResultType.BadRequest =>
                controller.BadRequest(response),

            ActionResultType.Unauthorized =>
                controller.Unauthorized(response),

            ActionResultType.Forbidden =>
                controller.StatusCode(
                    StatusCodes.Status403Forbidden,
                    response),

            ActionResultType.NotFound =>
                controller.NotFound(response),

            ActionResultType.Conflict =>
                controller.Conflict(response),

            ActionResultType.PayloadTooLarge =>
                controller.StatusCode(
                    StatusCodes.Status413PayloadTooLarge,
                    response),

            ActionResultType.UnsupportedMediaType =>
                controller.StatusCode(
                    StatusCodes.Status415UnsupportedMediaType,
                    response),

            ActionResultType.ClientClosedRequest =>
                controller.StatusCode(
                    StatusCodes.Status499ClientClosedRequest,
                    response),

            ActionResultType.BadGateway =>
                controller.StatusCode(
                    StatusCodes.Status502BadGateway,
                    response),

            ActionResultType.ServiceUnavailable =>
                controller.StatusCode(
                    StatusCodes.Status503ServiceUnavailable,
                    response),

            ActionResultType.InternalServerError =>
                controller.StatusCode(
                    StatusCodes.Status500InternalServerError,
                    response),

            _ => controller.StatusCode(
                StatusCodes.Status500InternalServerError,
                response)
        };
    }

    public static ActionResult<T> GetActionResult<T>(
        this ControllerBase controller,
        OneOf<T, ServiceResultContext> result)
    {
        return result.TryPickT0(out var value, out var error) ?
            controller.Ok(value) : controller.GetActionResult(error);
    }
}
