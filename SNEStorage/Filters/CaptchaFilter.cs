using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SNEStorage.DTOs;
using SNEStorage.Filters.Attributes;
using SNEStorage.Services;
using System.Security.Claims;

namespace SNEStorage.Filters;

public class CaptchaFilter(
    CaptchaService captchaService,
    CaptchaFrequencyService frequencyService,
    string expectedAction,
    CaptchaPolicy policy,
    bool allowFormToken) : IAsyncResourceFilter
{
    private readonly CaptchaService _captchaService = captchaService;
    private readonly CaptchaFrequencyService _frequencyService = frequencyService;
    private readonly string _expectedAction = expectedAction;
    private readonly CaptchaPolicy _policy = policy;
    private readonly bool _allowFormToken = allowFormToken;

    public async Task OnResourceExecutionAsync(
        ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var request = context.HttpContext.Request;
        var ct = context.HttpContext.RequestAborted;
        string? token = request.Headers["X-Captcha-Token"].ToString();

        if (string.IsNullOrWhiteSpace(token) &&
            _allowFormToken && request.HasFormContentType)
        {
            var form = await request.ReadFormAsync(ct);
            token = form["cf-turnstile-response"].ToString();
        }

        string? userId = context.HttpContext.User
            .FindFirstValue(ClaimTypes.NameIdentifier);

        bool conditional = _policy is not CaptchaPolicy.Always;

        if (string.IsNullOrWhiteSpace(token) && conditional &&
            !string.IsNullOrWhiteSpace(userId) &&
            _frequencyService.TryAllowWithoutCaptcha(userId, _policy))
        {
            await next();
            return;
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            context.Result = new BadRequestObjectResult(new
            {
                error = "CaptchaRequired",
                action = _expectedAction
            });

            return;
        }

        var result = await _captchaService.ValidateAsync(
            token, _expectedAction, ct);

        if (result.Result == ServiceResult.Valid)
        {
            await next();
            return;
        }

        bool invalid = result.Result == ServiceResult.Invalid;

        context.Result = new ObjectResult(new
        {
            error = invalid ? "CaptchaInvalid" : "CaptchaUnavailable",
            action = _expectedAction
        })
        {
            StatusCode = result.Result.ActionType switch
            {
                ActionResultType.BadRequest =>
                    StatusCodes.Status400BadRequest,

                ActionResultType.ClientClosedRequest =>
                    StatusCodes.Status499ClientClosedRequest,

                _ =>
                    StatusCodes.Status503ServiceUnavailable
            }
        };
    }
}
