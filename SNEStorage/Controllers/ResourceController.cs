using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Filters.Attributes;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/resource")]
public class ResourceController(SnestorageContext context,
    CacheService cacheService,
    ResourceService resourceService,
    FileController fileController,
    GetLikeController getLikeController,
    IWebHostEnvironment Environment) : Controller
{
    private readonly SnestorageContext _context = context;
    private readonly CacheService _cacheService = cacheService;
    private readonly ResourceService _resourceService = resourceService;
    private readonly FileController _fileController = fileController;
    private readonly GetLikeController _getLikeController = getLikeController;
    private readonly IWebHostEnvironment _environment = Environment;
    [EnableRateLimiting("reads")]
    [HttpGet("find")]
    public async Task<ActionResult<List<ResourceFinderViewDTO>>> Find([FromQuery] ResourceFinderDTO dto,
        [FromQuery] int maxAmount, [FromQuery] int page, CancellationToken ct = default)
    {
        var result = await _resourceService.Find(dto, maxAmount, page, ct);
        return GetActionResult(result);
    }
    [EnableRateLimiting("comments")]
    [HttpPost("{resourceId}/Comment/Submit")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("resource_addComment", CaptchaPolicy.After5TriesInShortTime)]
    public async Task<ActionResult<ResourceComments>> AddComment([FromRoute] long resourceId, [FromForm, StringLength(5000)] string commentText, CancellationToken ct)
    {
        var res = await _resourceService.AddComment(resourceId, commentText, ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("votes")]
    [HttpPost("{resourceId}/Score/Submit/{value}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("resource_addOrEditScore", CaptchaPolicy.After10TriesInShortTime)]
    public async Task<ActionResult<ScoreUsers>> AddOrEditScore([FromRoute] long resourceId, [FromRoute] long value, CancellationToken ct)
    {
        var res = await _resourceService.AddOrEditScore(resourceId, value, ct);
        return GetActionResult(res);
    }

    [EnableRateLimiting("reads")]
    [HttpGet("options")]
    public async Task<ActionResult<ResourceCreateOptionsDto>> GetCreateOptions(CancellationToken ct)
    {
        var resourceTypes = await _cacheService.GetAllTypes(ct);
        if (resourceTypes == null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        var visibilities = await _cacheService.GetAllVisibilities(ct);
        if (visibilities == null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);
        var categories = await _cacheService.GetAllCategories(ct);
        if(categories == null)
            return StatusCode(StatusCodes.Status503ServiceUnavailable);

        var options = new ResourceCreateOptionsDto
        {
            ResourceTypes = [..resourceTypes
                .OrderBy(r => r.Value.Name)
                .Select(r => new ResourceTypeDTO
                {
                    Id = r.Value.Id,
                    Name = r.Value.Name!,
                    CategoryId = r.Value.CategoryId,
                    CategoryName = r.Value.Category!.Name
                })],
            Visibilities = [..visibilities
                .OrderBy(v => v.Value.Id)
                .Select(v => new IdNamePair
                {
                    Id = v.Value.Id,
                    Name = v.Value.Name!
                })],
            Categories = [..categories
                .OrderBy(v => v.Value.Id)
                .Select(v => new IdNamePair
                {
                    Id = v.Value.Id,
                    Name = v.Value.Name!
                })]
        };

        return Ok(options);
    }
    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId}")]
    public async Task<ActionResult<ResourceReadDTO>> Get([FromRoute] long resourceId, CancellationToken ct = default)
    {
        var res = await _resourceService.Get(resourceId, ct: ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("uploads")]
    [HttpPut("{resourceId}/edit")]
    [RequestSizeLimit(110L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5L * 1024 * 1024)]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("resource_edit")]
    public async Task<ActionResult> Edit([FromRoute] long resourceId, [FromForm] ResourceEditDTO dto, CancellationToken ct = default)
    {
        var res = await _resourceService.Edit(resourceId, dto, ct: ct);

        return GetActionResult(res);
    }
    [EnableRateLimiting("writes")]
    [HttpPut("{resourceId}/version/{versionId}/edit")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequireCaptcha("resource_editVersion")]
    public async Task<ActionResult> EditVersion([FromRoute] long resourceId, [FromRoute] long versionId, [FromForm] ResourceVersionEditDTO dto, CancellationToken ct = default)
    {
        var res = await _resourceService.EditVersion(resourceId, versionId, dto, ct: ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId:long}/previews")]
    public async Task<ActionResult<List<ResourcePreview>>> GetPreviews([FromRoute] long resourceId, CancellationToken ct = default)
    {
        var res = await _resourceService.GetResourceAllPreviews(resourceId, ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId:long}/file/{resourceFileId:long}")]
    public async Task<ActionResult<Models.File>> GetFile([FromRoute] long resourceId, [FromRoute] long resourceFileId, CancellationToken ct = default)
    {
        var res = await _resourceService.GetFile(resourceId, resourceFileId, ct: ct);
        return GetActionResult(res);
    }
    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId}/file/{resourceFileId}/download")]
    public async Task<ActionResult> Download([FromRoute] long resourceId, [FromRoute] long resourceFileId, CancellationToken ct = default)
    {
        var res = await _resourceService.Download(resourceId, resourceFileId, ct);
        if (res.TryPickT0(out var file, out var error))
            return PhysicalFile(file.Item1, file.Item2, Path.GetFileName(file.Item1), true);
        return GetActionResult(error);
    }
    [EnableRateLimiting("reads")]
    [HttpGet("search")]
    public async Task<ActionResult<List<Resource>>> GetResourcesLike([FromQuery] string likeString, [FromQuery] int maxAmount = 10, CancellationToken ct = default)
    {
        var resources = await _resourceService.GetResourceLike(likeString, maxAmount, ct);
        return _getLikeController.GetActionResult(resources);
    }
    [EnableRateLimiting("uploads")]
    [HttpPost("create/submit")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequestSizeLimit(360L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 250L * 1024 * 1024)]
    [RequireCaptcha("resource_create")]
    public async Task<ActionResult<Resource>> CreateResource([FromForm] ResourceCreateDTO dto, CancellationToken ct)
    {
        var result = await _resourceService.Create(dto, ct);
        return GetActionResult(result);
    }
    [EnableRateLimiting("uploads")]
    [HttpPost("{resourceId}/version/create/submit")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequestSizeLimit(360L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 250L * 1024 * 1024)]
    [RequireCaptcha("resource_createVersion")]
    public async Task<ActionResult<ResourceVersion>> CreateVersion([FromRoute] long resourceId, [FromForm] ResourceVersionCreateDTO dto, CancellationToken ct)
    {
        var result = await _resourceService.AddVersion(resourceId, dto, ct);
        return GetActionResult(result);
    }
    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId:long}/dependencies")]
    public async Task<ActionResult<List<ResourceDependencyViewDTO>>> GetDependencies([FromRoute] long resourceId, CancellationToken ct = default)
    {
        var result = await _resourceService.GetResourceAllDependencies(resourceId, ct);
        return GetActionResult(result);
    }

    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId:long}/external-dependencies")]
    public async Task<ActionResult<List<ResourceExternalDependency>>> GetExternalDependencies([FromRoute] long resourceId, CancellationToken ct = default)
    {
        var result = await _resourceService.GetResourceAllExternalDependencies(resourceId, ct);
        return GetActionResult(result);
    }

    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId:long}/authors")]
    public async Task<ActionResult<List<ResourceAuthorDTO>>> GetAuthors([FromRoute] long resourceId, CancellationToken ct = default)
    {
        var result = await _resourceService.GetResourceAllAuthors(resourceId, ct);
        return GetActionResult(result);
    }

    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId:long}/external-authors")]
    public async Task<ActionResult<List<ResourceExternalAuthor>>> GetExternalAuthors([FromRoute] long resourceId, CancellationToken ct = default)
    {
        var result = await _resourceService.GetResourceAllExternalAuthors(resourceId, ct);
        return GetActionResult(result);
    }

    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId:long}/versions")]
    public async Task<ActionResult<List<ResourceVersion>>> GetVersions([FromRoute] long resourceId, CancellationToken ct = default)
    {
        var result = await _resourceService.GetResourceAllVersion(resourceId, ct);
        return GetActionResult(result);
    }

    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId:long}/versions/latest")]
    public async Task<ActionResult<ResourceVersion>> GetLatestVersion([FromRoute] long resourceId, CancellationToken ct = default)
    {
        var result = await _resourceService.GetResourceLastVersion(resourceId, ct);
        return GetActionResult(result);
    }

    [EnableRateLimiting("reads")]
    [HttpGet("{resourceId:long}/rating")]
    public async Task<ActionResult<RatingReadDto>> GetRating([FromRoute] long resourceId, CancellationToken ct = default)
    {
        var result = await _resourceService.GetRating(resourceId, ct);
        return GetActionResult(result);
    }
    private ObjectResult GetActionResult(ResourceServiceResult result)
    {
        string res = $"{GetType().Name}-{result}";
        bool authenticated =
            HttpContext?.User.Identity?.IsAuthenticated == true;

        return result switch
        {
            ResourceServiceResult.Success => Ok(res),

            ResourceServiceResult.FileNotFound or
            ResourceServiceResult.ResourceNotFound or
            ResourceServiceResult.ResourceTypeNotFound or
            ResourceServiceResult.VersionNotFound =>
                NotFound(res),

            ResourceServiceResult.Unauthorized when authenticated =>
                StatusCode(StatusCodes.Status403Forbidden, res),

            ResourceServiceResult.Unauthorized =>
                Unauthorized(res),

            ResourceServiceResult.UserNotFound when authenticated =>
                BadRequest(res),

            ResourceServiceResult.UserNotFound =>
                Unauthorized(res),

            ResourceServiceResult.CantAddScoreToPrivateResource or
            ResourceServiceResult.CantScoreSelfResource =>
                StatusCode(StatusCodes.Status403Forbidden, res),

            ResourceServiceResult.VersionAlreadyExists =>
                Conflict(res),

            ResourceServiceResult.PreviewSizeSurpassTheLimit or
            ResourceServiceResult.FileSizeSurpassTheLimit =>
                StatusCode(StatusCodes.Status413PayloadTooLarge, res),

            ResourceServiceResult.FileCreationError or
            ResourceServiceResult.BatchCreationError or
            ResourceServiceResult.FileDeletionError or
            ResourceServiceResult.DBError or
            ResourceServiceResult.CacheError =>
                StatusCode(StatusCodes.Status500InternalServerError, res),

            ResourceServiceResult.InvalidMaxAmount or
            ResourceServiceResult.SkipOutOfRange or
            ResourceServiceResult.MaxAmountSurpassTheLimit or
            ResourceServiceResult.NegativePage or
            ResourceServiceResult.InvalidScore or
            ResourceServiceResult.EmptyComment or
            ResourceServiceResult.PreviewAmountSurpassTheLimit or
            ResourceServiceResult.EmptyName or
            ResourceServiceResult.EmptyDescription or
            ResourceServiceResult.InvalidVersion or
            ResourceServiceResult.InvalidMaxAmount or
            ResourceServiceResult.InvalidName or
            ResourceServiceResult.InvalidRepositoryURL or
            ResourceServiceResult.InvalidDescription or
            ResourceServiceResult.AmountOfElementsSurpassTheLimit or
            ResourceServiceResult.InvalidReleaseNote or
            ResourceServiceResult.InvalidSort or
            ResourceServiceResult.InvalidScoreRange or
            ResourceServiceResult.InvalidDateRange or
            ResourceServiceResult.EmptyTypeList =>
                BadRequest(res),

            _ => StatusCode(StatusCodes.Status500InternalServerError, res)
        };
    }
    private ActionResult<T> GetActionResult<T>(OneOf<T, ResourceServiceResult> oneof)
    {
        return oneof.TryPickT0(out var item, out var error) ?
            Ok(item) :
            GetActionResult(error);
    }
    private ActionResult<T> GetActionResult<T>(OneOf<T, ResourceServiceResult, FileServiceResult> oneof)
    {
        return oneof.TryPickT0(out var item, out var error) ?
            Ok(item) :
            error.TryPickT0(out var error1, out var error2) ?
                GetActionResult(error1) :
                _fileController.GetActionResult(error2);
    }
    private ActionResult GetActionResult(OneOf<ResourceServiceResult, FileServiceResult> oneof)
    {
        return oneof.TryPickT0(out var res1, out var res2) ?
            GetActionResult(res1) :
            _fileController.GetActionResult(res2);
    }
}
