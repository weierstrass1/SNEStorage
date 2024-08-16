using System;
using System.Collections.Generic;

namespace SNEStorage.Models;

public partial class ResourceType
{
    public long Id { get; set; }

    public string? Name { get; set; }

    public virtual ICollection<Resource> Resources { get; set; } = new List<Resource>();
}
