using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using SNEStorage.Models;

namespace SNEStorage.Controllers;
public class ResourceController(SnestorageContext context, IWebHostEnvironment Environment) : Controller
{

    




    public async Task<IActionResult> IndexAsync()
    {
        List<Resource> resources = await context.Resources
            .Include(x => x.ResourceType)
            .Include(x => x.Videogame)
            .Include(x=>x.SubmitterUser)
            .AsNoTracking()
            .ToListAsync();
        return View(resources);
    }

    
    public async Task<IActionResult> Download(long id)
    {
        var res = await context.Resources
            .Include(r => r.File)
                .ThenInclude(f => f.FileType) // si tienes mime en FileType.Value
            .FirstOrDefaultAsync(r => r.Id == id);

        if (res is null || res.File is null || string.IsNullOrWhiteSpace(res.File.URL))
            return NotFound();

        var relative = res.File.URL.TrimStart('/', '\\'); // p.ej. "resources/2025/09/abcd.ext"
        var physical = Path.Combine(Environment.WebRootPath, relative);

        if (!System.IO.File.Exists(physical))
            return NotFound();

        // Nombre de descarga: usa Name + extensión real del archivo
        var baseName = string.IsNullOrWhiteSpace(res.Name)
            ? Path.GetFileNameWithoutExtension(physical)
            : res.Name;
        var safeBase = string.Concat(baseName.Split(Path.GetInvalidFileNameChars()));
        var ext = Path.GetExtension(physical);
        var downloadName = safeBase + ext;

        // Content-Type
        string? contentType = res.File.FileType?.Value;
        if (string.IsNullOrEmpty(contentType))
        {
            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(physical, out contentType))
                contentType = "application/octet-stream";
        }

        // Contador de descargas
        res.Downloads = (res.Downloads ?? 0) + 1;
        await context.SaveChangesAsync();

        // Devuelve streaming desde disco, con rangos
        return PhysicalFile(physical, contentType, downloadName, enableRangeProcessing: true);
    }
}
