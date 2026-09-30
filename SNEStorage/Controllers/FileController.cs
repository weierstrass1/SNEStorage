using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SNEStorage.DTOs;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/file")]
public sealed class FileController(FileService files, ResourceAccessService access) : Controller
{
    [HttpPost]
    public async Task<ActionResult<FileResponse>> Create(IFormFile fileRequest, string destinationPath)
    {
        if (!access.CanUpload(User))
            return Forbid();

        var result = await files.Create(fileRequest, destinationPath);
        return result is null ? BadRequest() : result;
    }
}
