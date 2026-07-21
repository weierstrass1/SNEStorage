using System;
using System.ComponentModel.DataAnnotations;

namespace SNEStorage.Models
{
    public class ResourceFile
    {
        public int Id { get; set; }
        
        [Required, MaxLength(20)]
        public string Version { get; set; }
        
        [Required]
        public string FileName { get; set; }
        
        public long FileSizeBytes { get; set; }
        
        [Required]
        public string FileHash { get; set; }
        
        public DateTime UploadDate { get; set; } = DateTime.UtcNow;

        public int ResourceId { get; set; }
        public Resource Resource { get; set; }
    }
}
