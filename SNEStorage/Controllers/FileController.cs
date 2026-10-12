using Microsoft.AspNetCore.Mvc;
using SNEStorage.Services;

namespace SNEStorage.Controllers;
public class FileController : Controller
{
    [NonAction]
    public ObjectResult GetActionResult(FileServiceResult result)
    {
        string res = $"{GetType().Name}-{result}";
        return result switch
        {
            FileServiceResult.Success => Ok(res),

            FileServiceResult.MediaSizeSurpassTheLimit or
            FileServiceResult.CompressedFileSizeSurpassTheLimit or
            FileServiceResult.FileSizeSurpassTheLimit or
            FileServiceResult.AvatarSizeSurpassTheLimit =>
                StatusCode(StatusCodes.Status413PayloadTooLarge, res),

            FileServiceResult.InvalidMime or
            FileServiceResult.MimeMismatch =>
                StatusCode(StatusCodes.Status415UnsupportedMediaType, res),

            FileServiceResult.ImageSurpassFramesLimit or
            FileServiceResult.InvalidImageSize or
            FileServiceResult.InvalidImageNumberOfFrames or
            FileServiceResult.InvalidImage or
            FileServiceResult.FormReadingError or
            FileServiceResult.InvalidVideo or
            FileServiceResult.InvalidFile or
            FileServiceResult.InvalidFileForContext or
            FileServiceResult.EmptyFile =>
                BadRequest(res),

            FileServiceResult.CancellationTokenError
                when HttpContext?.RequestAborted.IsCancellationRequested == true =>
                StatusCode(StatusCodes.Status499ClientClosedRequest, res),

            FileServiceResult.FileCreationError or
            FileServiceResult.CacheError or
            FileServiceResult.BatchError or
            FileServiceResult.DBError or
            FileServiceResult.UpdateBatchError or
            FileServiceResult.CancellationTokenError =>
                StatusCode(StatusCodes.Status500InternalServerError, res),

            _ => StatusCode(StatusCodes.Status500InternalServerError, res)
        };
    }
}
