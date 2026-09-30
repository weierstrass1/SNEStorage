using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SNEStorage.DTOs.ViewModels;
using SNEStorage.Models;
using SNEStorage.Services;
using System;
using System.Security.Claims;

namespace SNEStorage.Controllers
{
    public class ResourcesController : Controller
    {
        private readonly SnestorageContext _context;
        private readonly FileService _fService;

        public ResourcesController(SnestorageContext context, FileService fService)
        {
            _context = context;
            _fService = fService;
        }

        // GET: Recursos/Create
        public async Task<IActionResult> Create()
        {
            var model = new RecursoViewModel
            {
                VideogameOptions = _context.Videogames
                    .Select(v => new SelectListItem
                    {
                        Value = v.Id.ToString(),
                        Text = v.Name
                    }).ToList(),

                ResourceTypeOptions = _context.ResourceTypes
                    .Select(r => new SelectListItem
                    {
                        Value = r.Id.ToString(),
                        Text = r.Name
                    }).ToList(),

                VisibilityOptions = _context.Visibilities
                    .Select(v => new SelectListItem
                    {
                        Value = v.Id.ToString(),
                        Text = v.Name
                    }).ToList()
            };

            return View(model);
        }

        // POST: Recursos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(RecursoViewModel model, CancellationToken ct)
        {
            // Repoblar selects siempre que retornes la vista
            async Task HydrateSelects()
            {
                model.VideogameOptions = await _context.Videogames
                    .Select(v => new SelectListItem { Value = v.Id.ToString(), Text = v.Name })
                    .ToListAsync(ct);
                model.ResourceTypeOptions = await _context.ResourceTypes
                    .Select(r => new SelectListItem { Value = r.Id.ToString(), Text = r.Name })
                    .ToListAsync(ct);
                model.VisibilityOptions = await _context.Visibilities
                    .Select(v => new SelectListItem { Value = v.Id.ToString(), Text = v.Name })
                    .ToListAsync(ct);
            }

            await HydrateSelects();

            if (model.File is null || model.File.Length == 0)
            {
                ModelState.AddModelError(nameof(model.File), "Archivo requerido.");
                return View(model);
            }

            // Ruta relativa dentro de wwwroot: resources/yyyy/MM/guid.ext
            var ext = Path.GetExtension(model.File.FileName);
            var relativePath = Path.Combine(
                "resources",
                DateTime.UtcNow.ToString("yyyy"),
                DateTime.UtcNow.ToString("MM"),
                $"{Guid.NewGuid():N}{ext}"
            ).Replace('\\', '/');

            // 1) Guardar físico + fila en [file]
            var saved = await _fService.CreateAsync(model.File, relativePath, ct);
            if (saved is null)
            {
                ModelState.AddModelError(nameof(model.File), "Tipo de archivo no soportado.");
                return View(model);
            }

            // 2) Score mínimo (ajusta si ya manejas uno por defecto)
            var score = new Score();
            _context.Scores.Add(score);
            await _context.SaveChangesAsync(ct);

            // 3) Insert en [resource]
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                         ?? throw new InvalidOperationException("Usuario no autenticado.");

            var entity = new Resource
            {
                Name = model.Name,
                VideogameId = model.VideogameId,
                ResourceTypeId = model.ResourceTypeId,
                Usage = model.Usage,
                Description = model.Description,
                Version = model.Version,
                Downloads = 0,
                IncludesGore = model.IncludesGore,
                IncludesPolitics = model.IncludesPolitics,
                IncludesPorn = model.IncludesPorn,
                IncludesSensitiveContent = model.IncludesSensitiveContent,
                IncludesSlurs = model.IncludesSlurs,
                VisibilityId = model.VisibilityId,
                FileId = saved.Id,                 // FK a [file]
                SubmitterUserId = userId,
                ScoreId = score.Id,
                CreatedAt = DateTime.UtcNow,
                LastUpdate = DateTime.UtcNow
            };

            _context.Resources.Add(entity);
            await _context.SaveChangesAsync(ct);
            return RedirectToAction(nameof(Index));
        }
    }
}
