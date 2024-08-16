using System;
using System.Collections.Generic;

namespace SNEStorage.Models;

public partial class Resource
{
    public long Id { get; set; }

    public string? Name { get; set; }

    public long? VideogameId { get; set; }

    public long? ResourceTypeId { get; set; }

    public string? Usage { get; set; }

    public string? Description { get; set; }

    public string? Version { get; set; }

    public int? Downloads { get; set; }

    public string? Url { get; set; }

    public virtual ResourceType? ResourceType { get; set; }

    public virtual Videogame? Videogame { get; set; }
}
