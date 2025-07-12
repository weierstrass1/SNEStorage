using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using SNEStorage.DTOs.ViewModels;
using SNEStorage.Models;
using System;

namespace SNEStorage.Controllers
{
    public class ResourcesController : Controller
    {
        private readonly SnestorageContext _context;

        public ResourcesController(SnestorageContext context)
        {
            _context = context;
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
        public async Task<IActionResult> Create(RecursoViewModel model, IFormFile File)
        {
            if (!ModelState.IsValid)
            {
                // Repoblar selects si algo falla
                model.VideogameOptions = _context.Videogames
                    .Select(v => new SelectListItem { Value = v.Id.ToString(), Text = v.Name }).ToList();
                model.ResourceTypeOptions = _context.ResourceTypes
                    .Select(r => new SelectListItem { Value = r.Id.ToString(), Text = r.Name }).ToList();
                model.VisibilityOptions = _context.Visibilities
                    .Select(v => new SelectListItem { Value = v.Id.ToString(), Text = v.Name }).ToList();

                return View(model);
            }

            // Acá puedes procesar el archivo y guardar en la base de datos
            // TODO: Implementar lógica de guardado

            return RedirectToAction("Index"); // o a donde quieras
        }
    }
}
