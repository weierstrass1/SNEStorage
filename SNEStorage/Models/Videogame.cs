using System;
using System.Collections.Generic;

namespace SNEStorage.Models;

public partial class Videogame
{
    public long Id { get; set; }

    public string Name { get; set; } = null!;

    public DateOnly? ReleaseDate { get; set; }

    public string? Publisher { get; set; }

    public virtual ICollection<Resource> Resources { get; set; } = new List<Resource>();
}
