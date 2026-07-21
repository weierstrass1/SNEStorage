using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;

namespace SNEStorage.Models
{
    public class User : IdentityUser<int>
    {
        public DateTime JoinDate { get; set; } = DateTime.UtcNow;

        // Navigation property
        public ICollection<Resource> Resources { get; set; } = new List<Resource>();
    }
}
