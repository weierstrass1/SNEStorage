using Microsoft.AspNetCore.Mvc.Rendering;

namespace SNEStorage.DTOs.ViewModels
{
    public class RecursoViewModel
    {
        public string Name { get; set; }
        public long? VideogameId { get; set; }
        public long? ResourceTypeId { get; set; }
        public string Usage { get; set; }
        public string Description { get; set; }
        public string Version { get; set; }

        public bool IncludesGore { get; set; }
        public bool IncludesPolitics { get; set; }
        public bool IncludesPorn { get; set; }
        public bool IncludesSensitiveContent { get; set; }
        public bool IncludesSlurs { get; set; }

        public long VisibilityId { get; set; }

        // Para selects
        public IEnumerable<SelectListItem> VideogameOptions { get; set; }
        public IEnumerable<SelectListItem> ResourceTypeOptions { get; set; }
        public IEnumerable<SelectListItem> VisibilityOptions { get; set; }

        public IFormFile File { get; set; }  // con setter público
    }

}
