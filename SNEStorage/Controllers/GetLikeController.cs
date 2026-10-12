using Microsoft.AspNetCore.Mvc;
using OneOf;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

public class GetLikeController : Controller
{
    [NonAction]
    public ActionResult GetActionResult(GetLikeResult result)
    {
        string res = $"{GetType().Name}-{result}";
        return result switch
        {
            GetLikeResult.Success => Ok(res),

            GetLikeResult.InvalidMaxAmount or
            GetLikeResult.MaxAmountSurpassTheLimit =>
                BadRequest(res),

            GetLikeResult.DBError =>
                StatusCode(StatusCodes.Status500InternalServerError, res),

            _ => StatusCode(StatusCodes.Status500InternalServerError, res)
        };
    }
    public ActionResult<T> GetActionResult<T>(OneOf<T, GetLikeResult> result)
    {
        if (result.TryPickT0(out var item, out var error))
            return Ok(item);
        return GetActionResult(error);
    }
}