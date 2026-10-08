using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/resource")]
public class ResourceController(SnestorageContext context,
    ResourceService resourceService,
    IWebHostEnvironment Environment) : Controller
{
    private readonly SnestorageContext _context = context;
    private readonly ResourceService _resourceService = resourceService;
    private readonly IWebHostEnvironment _environment = Environment;
    private ActionResult<ResourceServiceResult> GetActionResult(ResourceServiceResult result)
    {
        return result switch
        {
            ResourceServiceResult.Success => Ok(result),
            ResourceServiceResult.FileNotFound or
            ResourceServiceResult.ResourceNotFound or
            ResourceServiceResult.ResourceTypeNotFound or
            ResourceServiceResult.VersionNotFound => NotFound(result),
            ResourceServiceResult.Unauthorized or
            ResourceServiceResult.UserNotFound => Unauthorized(result),
            _ => BadRequest(result)
        };
    }
    private ActionResult<OneOf<T, ResourceServiceResult>> GetActionResult<T>(ResourceServiceResult result)
    {
        return result switch
        {
            ResourceServiceResult.Success => Ok(result),
            ResourceServiceResult.FileNotFound or
            ResourceServiceResult.ResourceNotFound or
            ResourceServiceResult.ResourceTypeNotFound or
            ResourceServiceResult.VersionNotFound => NotFound(result),
            ResourceServiceResult.Unauthorized or
            ResourceServiceResult.UserNotFound => Unauthorized(result),
            _ => BadRequest(result)
        };
    }
    [HttpPost("{resourceId}/Comment/Submit")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<OneOf<ResourceComments,ResourceServiceResult>>> AddComment([FromRoute] long resourceId, [FromForm] string commentText, CancellationToken ct)
    {
        var res = await _resourceService.AddComment(resourceId, commentText, ct);
        if (res.TryPickT0(out var resourceComments, out var error))
            return Ok(resourceComments);
        return GetActionResult<ResourceComments>(error);
    }
    [HttpPost("{resourceId}/Score/Submit/{value}")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<OneOf<ScoreUsers, ResourceServiceResult>>> AddOrEditScore([FromRoute] long resourceId, [FromRoute] long value, CancellationToken ct)
    {
        var res = await _resourceService.AddOrEditScore(resourceId, value, ct);
        if (res.TryPickT0(out var scoreUsers, out var error))
            return Ok(scoreUsers);
        return GetActionResult<ScoreUsers>(error);
    }

    [HttpGet("options")]
    public async Task<ActionResult<ResourceCreateOptionsDto>> GetCreateOptions(CancellationToken ct)
    {
        var options = new ResourceCreateOptionsDto
        {
            ResourceTypes = await _context.ResourceTypes.AsNoTracking()
                .OrderBy(r => r.Name)
                .Select(r => new ResourceTypeDTO 
                { 
                    Id = r.Id, 
                    Name = r.Name!,
                    CategoryId = r.CategoryId,
                    CategoryName = r.Category!.Name
                })
                .ToListAsync(ct),
            Visibilities = await _context.Visibilities.AsNoTracking()
                .OrderBy(v => v.Id)
                .Select(v => new SelectOptionDto { Id = v.Id, Name = v.Name })
                .ToListAsync(ct)
        };

        return Ok(options);
    }
    [HttpGet("{resourceId}")]
    public async Task<ActionResult<OneOf<Resource, ResourceServiceResult>>> Get([FromRoute]long resourceId, CancellationToken ct = default)
    {
        var res = await _resourceService.Get(resourceId, ct: ct);
        if (res.TryPickT0(out var resource, out var error))
            return Ok(resource);

        return GetActionResult<Resource>(error);
    }
    [HttpPut("{resourceId}/edit")]
    [RequestSizeLimit(110L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 5L * 1024 * 1024)]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<ResourceServiceResult>> Edit([FromRoute] long resourceId, [FromForm] ResourceEditDTO dto, CancellationToken ct = default)
    {
        var res = await _resourceService.Edit(resourceId, dto, ct: ct);

        return GetActionResult(res);
    }
    [HttpPut("{resourceId}/version/{versionId}/edit")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<ResourceServiceResult>> EditVersion([FromRoute] long resourceId, [FromRoute] long versionId, [FromForm] ResourceVersionEditDTO dto, CancellationToken ct = default)
    {
        var res = await _resourceService.EditVersion(resourceId, versionId, dto, ct: ct);

        return GetActionResult(res);
    }
    [HttpGet("{resourceId}/file/{resourceFileId}")]
    public async Task<ActionResult<OneOf<Resource, ResourceServiceResult>>> GetFile([FromRoute] long resourceId, [FromRoute] long resourceFileId, CancellationToken ct = default)
    {
        var res = await _resourceService.GetFile(resourceId, resourceFileId, ct: ct);
        if (res.TryPickT0(out var file, out var error))
            return Ok(file);
        return GetActionResult<Resource>(error);
    }
    [HttpGet("{resourceId}/file/{resourceFileId}/download")]
    public async Task<ActionResult<ResourceServiceResult>> Download([FromRoute] long resourceId, [FromRoute] long resourceFileId, CancellationToken ct = default)
    {
        var res = await _resourceService.Download(resourceId, resourceFileId, ct);
        if (res.TryPickT0(out var file, out var error))
            return PhysicalFile(file.Item1, file.Item2, Path.GetFileName(file.Item1), true);
        return GetActionResult(error);
    }
    [HttpGet("search")]
    public async Task<ActionResult<List<Resource>>> GetResourcesLike([FromQuery] string likeString, [FromQuery] int maxAmount = 10, CancellationToken ct = default)
    {
        var resources = await _resourceService.GetResourceLike(likeString, maxAmount, ct);
        return Ok(resources);
    }
    [HttpPost("create/submit")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequestSizeLimit(360L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 250L * 1024 * 1024)]
    public async Task<ActionResult<OneOf<Resource, ResourceServiceResult>>> CreateResource([FromForm] ResourceCreateDTO dto, CancellationToken ct)
    {
        var res = await _resourceService.Create(dto, ct);
        if (res.TryPickT0(out var response, out var error))
            return Ok(response);
        return GetActionResult<Resource>(error);
    }
    [HttpPost("{resourceId}/version/create/submit")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    [RequestSizeLimit(360L * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 250L * 1024 * 1024)]
    public async Task<ActionResult<OneOf<ResourceVersion,ResourceServiceResult>>> CreateVersion([FromRoute] long resourceId, [FromForm] ResourceVersionCreateDTO dto, CancellationToken ct)
    {
        var res = await _resourceService.AddVersion(resourceId, dto, ct);
        if (res.TryPickT0(out var response, out var error))
            return Ok(response);
        return GetActionResult<ResourceVersion>(error);
    }
}
