using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneOf;
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
    [HttpGet("{commentId}/userlike")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<OneOf<LikeUsers, CommentServiceResult>>> GetUserLike([FromRoute] long commentId, CancellationToken ct = default)
    {
        var res = await _commentService.GetUserLike(commentId, ct);
        return GetActionResult(res);
    }
    [HttpPost("{commentId}/like")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<OneOf<LikeUsers, CommentServiceResult>>> AddEditOrDeleteLike([FromRoute] long commentId,
        [FromForm] LikeValues value, CancellationToken ct = default)
    {
        var res = await _commentService.AddEditOrDeleteLike(commentId, value, ct);
        return GetActionResult(res);
    }
    [HttpGet("{commentId}/replies")]
    public async Task<ActionResult<OneOf<List<Comment>, CommentServiceResult>>> GetReplies([FromRoute] long commentId,
        [FromQuery] int maxAmount = 20, [FromQuery] int page = 0,
        [FromQuery] CommentSorting sort = CommentSorting.RecentFirst,
    CancellationToken ct = default)
    {
        var res = await _commentService.GetReplies(commentId, maxAmount, page, sort, ct);
        return GetActionResult(res);
    }
    [HttpGet("resource/{resourceId}")]
    public async Task<ActionResult<OneOf<List<Comment>, CommentServiceResult>>> GetResourceComments([FromRoute] long resourceId,
        [FromQuery] int maxAmount = 20, [FromQuery] int page = 0,
        [FromQuery] CommentSorting sort = CommentSorting.RecentFirst,
        CancellationToken ct = default)
    {
        var res = await _commentService.GetResourceComments(resourceId, maxAmount, page, sort, ct);
        return GetActionResult(res);
    }
    [HttpPost("{commentId}/reply")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<OneOf<CommentComment, CommentServiceResult>>> Reply([FromRoute] long commentId, [FromForm] string commentText, CancellationToken ct = default)
    {
        var res = await _commentService.AddCommentToComment(commentId, commentText, ct);
        return GetActionResult(res);
    }
    [HttpDelete("{commentId}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<CommentServiceResult>> Delete([FromRoute] long commentId, CancellationToken ct = default)
    {
        var res = await _commentService.DeleteComment(commentId, ct);
        return GetActionResult(res);
    }
    [HttpPut("{commentId}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<CommentServiceResult>> Edit([FromRoute] long commentId, [FromForm] string commentText, CancellationToken ct = default)
    {
        var res = await _commentService.EditComment(commentId, commentText, ct);
        return GetActionResult(res);
    }
    private ActionResult<CommentServiceResult> GetActionResult(CommentServiceResult result)
    {
        return result switch
        {
            CommentServiceResult.Success => Ok(result),
            CommentServiceResult.Unauthorized => Unauthorized(result),
            CommentServiceResult.CommentNotFound or
            CommentServiceResult.ResourceNotFound or
            CommentServiceResult.LikeNotFound => NotFound(result),
            CommentServiceResult.DBError =>
                StatusCode(StatusCodes.Status500InternalServerError, result),
            _ => BadRequest(result)
        };
    }
    private ActionResult<OneOf<T, CommentServiceResult>> GetActionResult<T>(OneOf<T, CommentServiceResult> result)
    {
        if (result.TryPickT0(out T entity, out var error))
            return Ok(entity);
        return error switch
        {
            CommentServiceResult.Success => Ok(error),
            CommentServiceResult.Unauthorized => Unauthorized(error),
            CommentServiceResult.CommentNotFound or
            CommentServiceResult.ResourceNotFound or
            CommentServiceResult.LikeNotFound => NotFound(error),
            CommentServiceResult.DBError =>
                StatusCode(StatusCodes.Status500InternalServerError, error),
            _ => BadRequest(error)
        };
    }
}
