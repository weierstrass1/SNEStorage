using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SNEStorage.DTOs;
using SNEStorage.Models;
using System;

namespace SNEStorage.Controllers
{
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
        public async Task<ActionResult<FileResponse>> Create(FileParams pars)
        {
            FileType? type = await Context.FileTypes
                .FirstOrDefaultAsync(f => f.Value.ToString() == pars.File.ContentType);
            if (type == null)
                return BadRequest();
            string path = Path.Combine(Environment.WebRootPath, pars.DestinationPath);
            pars.File.CopyTo(new FileStream(path, FileMode.Create));

            Models.File file = new()
            {
                URL = pars.DestinationPath,
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
}
