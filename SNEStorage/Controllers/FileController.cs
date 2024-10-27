using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/file")]
public class FileController : Controller
{
    public SnestorageContext Context { get; }
    public IWebHostEnvironment Environment { get; }
    public FileService FileService { get; }

    public FileController(SnestorageContext context,
        IWebHostEnvironment environment,
        FileService fileService)
    {
        Context = context;
        Environment = environment;
        FileService = fileService;
    }
    [HttpPost]
    public async Task<ActionResult<FileResponse>> Create(IFormFile fileRequest, string destinationPath)
    {
        var res = await FileService.Create(fileRequest, destinationPath);
        return res == null ? BadRequest() : res;
    }
}
