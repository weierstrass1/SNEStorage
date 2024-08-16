using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SNEStorage.Models;

namespace SNEStorage.Controllers
{
    public class ResourceController(SnestorageContext context) : Controller
    {
        public async Task<IActionResult> Index()
        {
            List<Resource> resources = await context.Resources
                .Include(x => x.ResourceType)
                .Include(x => x.Videogame)
                .AsNoTracking()
                .ToListAsync();
            return View(resources);
        }
        public async Task<IActionResult> Download(long id)
        {
            Resource? res = await context.Resources.FindAsync(id);

            if (res == null)
                return File([], "application/x-rar-compressed", Path.GetFileName(res.Url));
            byte[] bytes;
            using (var client = new HttpClient())
            using (var result = await client.GetAsync(res.Url!))
                bytes = result.IsSuccessStatusCode ?
                    await result.Content.ReadAsByteArrayAsync() :
                    [];
            res.Downloads++;
            context.SaveChanges();

            return File(bytes, "application/x-rar-compressed", Path.GetFileName(res.Url));
        }
    }
}
