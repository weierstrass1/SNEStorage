using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/file")]
public class FileController : Controller
{
    public SnestorageContext Context { get; }
    public IWebHostEnvironment Environment { get; }

    public FileController(SnestorageContext context,
        IWebHostEnvironment environment)
    {
        Context = context;
        Environment = environment;
    }
    [HttpPost]
    public async Task<ActionResult<FileResponse>> Create(IFormFile fileRequest, string destinationPath)
    {
        string filetype = fileRequest.ContentType;

        FileType? type = await Context.FileTypes
            .FirstOrDefaultAsync(f => f.Value.Contains(filetype));
        if (type == null)
            return BadRequest();
        string path = Path.Combine(Environment.WebRootPath, destinationPath);

        using (FileStream stream = System.IO.File.Create(path))
        {
            await fileRequest.CopyToAsync(stream);
        }

        Models.File file = new()
        {
            URL = destinationPath,
            FileTypeId = type.Id
        };

        Context.Add(file);
        Context.SaveChanges();

        FileResponse result = new()
        {
            Id = file.Id,
            URL = file.URL,
            FileTypeId = file.FileTypeId
        };
        return result;
    }
}
