using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNEStorage.DTOs;
using SNEStorage.Filters.Attributes;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/comment")]
public class CommentController(CommentService commentService) : Controller
{
    private readonly CommentService _commentService = commentService;

    [EnableRateLimiting("reads")]
    [HttpGet("{commentId}/userlike")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<LikeUsers>> GetUserLike([FromRoute] long commentId, CancellationToken ct = default)
    {
        var res = await _commentService.GetUserLike(commentId, ct);
        return this.GetActionResult(res);
    }
    [EnableRateLimiting("votes")]
    [HttpPost("{commentId}/like")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("comment_AddEditOrDeleteLike", CaptchaPolicy.After10TriesInShortTime)]
    public async Task<ActionResult<LikeUsers>> AddEditOrDeleteLike([FromRoute] long commentId,
        [FromForm] LikeValues value, CancellationToken ct = default)
    {
        var res = await _commentService.AddEditOrDeleteLike(commentId, value, ct);
        return this.GetActionResult(res);
    }
    [EnableRateLimiting("reads")]
    [HttpGet("{commentId}/replies")]
    public async Task<ActionResult<List<CommentReadDto>>> GetReplies([FromRoute] long commentId,
        [FromQuery] int maxAmount = 20, [FromQuery] int page = 0,
        [FromQuery] CommentSorting sort = CommentSorting.RecentFirst,
        CancellationToken ct = default)
    {
        var res = await _commentService.GetReplies(commentId, maxAmount, page, sort, ct);
        return this.GetActionResult(res);
    }
    [EnableRateLimiting("reads")]
    [HttpGet("resource/{resourceId}")]
    public async Task<ActionResult<List<CommentReadDto>>> GetResourceComments([FromRoute] long resourceId,
        [FromQuery] int maxAmount = 20, [FromQuery] int page = 0,
        [FromQuery] CommentSorting sort = CommentSorting.RecentFirst,
        CancellationToken ct = default)
    {
        var res = await _commentService.GetResourceComments(resourceId, maxAmount, page, sort, ct);
        return this.GetActionResult(res);
    }
    [EnableRateLimiting("comments")]
    [HttpPost("{commentId}/reply")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("comment_reply", CaptchaPolicy.After5TriesInShortTime)]
    public async Task<ActionResult<CommentComment>> Reply([FromRoute] long commentId,
        [FromForm, StringLength(5000)] string commentText, CancellationToken ct = default)
    {
        var res = await _commentService.AddCommentToComment(commentId, commentText, ct);
        return this.GetActionResult(res);
    }
    [EnableRateLimiting("comments")]
    [HttpDelete("{commentId}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult> Delete([FromRoute] long commentId, CancellationToken ct = default)
    {
        var res = await _commentService.DeleteComment(commentId, ct);
        return this.GetActionResult(res);
    }
    [EnableRateLimiting("comments")]
    [HttpPut("{commentId}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("comment_edit", CaptchaPolicy.After5TriesInShortTime)]
    public async Task<ActionResult> Edit([FromRoute] long commentId,
        [FromForm, StringLength(5000)] string commentText, CancellationToken ct = default)
    {
        var res = await _commentService.EditComment(commentId, commentText, ct);
        return this.GetActionResult(res);
    }
}
