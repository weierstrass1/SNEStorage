using Microsoft.AspNetCore.Mvc;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

public class EmailController : Controller
{
    [NonAction]
    public ObjectResult GetActionResult(EmailServiceResult result)
    {
        string res = $"{GetType().Name}-{result}";

        return result switch
        {
            EmailServiceResult.Success => Ok(res),

            EmailServiceResult.UserNotFound => NotFound(res),

            EmailServiceResult.TemplateLoadError =>
                StatusCode(StatusCodes.Status500InternalServerError, res),

            EmailServiceResult.EmailConnectionError =>
                StatusCode(StatusCodes.Status503ServiceUnavailable, res),

            _ => StatusCode(StatusCodes.Status500InternalServerError, res)
        };
    }
}
