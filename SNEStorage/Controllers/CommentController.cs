using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Filters.Attributes;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/comment")]
public class CommentController(CommentService commentService,
    DBHelperService dbHelperService) : Controller
{
    private readonly CommentService _commentService = commentService;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    [EnableRateLimiting("reads")]
    [HttpGet("{commentId}/userlike")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<LikeUsers>> GetUserLike([FromRoute] long commentId, CancellationToken ct = default)
    {
        var res = await _commentService.GetUserLike(commentId, ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("votes")]
    [HttpPost("{commentId}/like")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("comment_AddEditOrDeleteLike", CaptchaPolicy.After10TriesInShortTime)]
    public async Task<ActionResult<LikeUsers>> AddEditOrDeleteLike([FromRoute] long commentId,
        [FromForm] LikeValues value, CancellationToken ct = default)
    {
        var res = await _commentService.AddEditOrDeleteLike(commentId, value, ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("reads")]
    [HttpGet("{commentId}/replies")]
    public async Task<ActionResult<List<CommentReadDto>>> GetReplies([FromRoute] long commentId,
        [FromQuery] int maxAmount = 20, [FromQuery] int page = 0,
        [FromQuery] CommentSorting sort = CommentSorting.RecentFirst,
    CancellationToken ct = default)
    {
        var res = await _commentService.GetReplies(commentId, maxAmount, page, sort, ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("reads")]
    [HttpGet("resource/{resourceId}")]
    public async Task<ActionResult<List<CommentReadDto>>> GetResourceComments([FromRoute] long resourceId,
        [FromQuery] int maxAmount = 20, [FromQuery] int page = 0,
        [FromQuery] CommentSorting sort = CommentSorting.RecentFirst,
        CancellationToken ct = default)
    {
        var res = await _commentService.GetResourceComments(resourceId, maxAmount, page, sort, ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("comments")]
    [HttpPost("{commentId}/reply")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("comment_reply", CaptchaPolicy.After5TriesInShortTime)]
    public async Task<ActionResult<CommentComment>> Reply([FromRoute] long commentId, [FromForm, StringLength(5000)] string commentText, CancellationToken ct = default)
    {
        var res = await _commentService.AddCommentToComment(commentId, commentText, ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("comments")]
    [HttpDelete("{commentId}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult> Delete([FromRoute] long commentId, CancellationToken ct = default)
    {
        var res = await _commentService.DeleteComment(commentId, ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("comments")]
    [HttpPut("{commentId}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("comment_edit", CaptchaPolicy.After5TriesInShortTime)]
    public async Task<ActionResult> Edit([FromRoute] long commentId, [FromForm, StringLength(5000)] string commentText, CancellationToken ct = default)
    {
        var res = await _commentService.EditComment(commentId, commentText, ct);
        return GetActionResult(res);
    }
    private ObjectResult GetActionResult(CommentServiceResult result)
    {
        string res = $"{GetType().Name}-{result}";
        bool authenticated =
            HttpContext?.User.Identity?.IsAuthenticated == true;

        return result switch
        {
            CommentServiceResult.Success => Ok(res),

            CommentServiceResult.Unauthorized when authenticated =>
                StatusCode(StatusCodes.Status403Forbidden, res),

            CommentServiceResult.Unauthorized =>
                Unauthorized(res),

            CommentServiceResult.CommentNotFound or
            CommentServiceResult.ResourceNotFound or
            CommentServiceResult.LikeNotFound =>
                NotFound(res),

            CommentServiceResult.CantLikeYourOwnComment =>
                StatusCode(StatusCodes.Status403Forbidden, res),

            CommentServiceResult.CantLikeDeletedComment or
            CommentServiceResult.CantReplyDeletedComment =>
                Conflict(res),

            CommentServiceResult.UndefinedLike or
            CommentServiceResult.UndefinedSort or
            CommentServiceResult.AmountOfCommentsSurpassTheLimit or
            CommentServiceResult.InvalidAmountOfCommentsPerPage or
            CommentServiceResult.NegativePage or
            CommentServiceResult.SkipOutOfRange or
            CommentServiceResult.EmptyComment =>
                BadRequest(res),

            CommentServiceResult.DBError =>
                StatusCode(StatusCodes.Status500InternalServerError, res),

            _ => StatusCode(StatusCodes.Status500InternalServerError, res)
        };
    }
    private ActionResult<T> GetActionResult<T>(OneOf<T, CommentServiceResult> result)
    {
        if (result.TryPickT0(out T entity, out var error))
            return Ok(entity);
        return GetActionResult(error);
    }
}
