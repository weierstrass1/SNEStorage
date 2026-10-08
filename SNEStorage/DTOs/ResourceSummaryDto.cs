namespace SNEStorage.DTOs;

public sealed class ResourceSummaryDto
{
    public long Id { get; set; }
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Videogame { get; set; }
    public string? Type { get; set; }
    public string? Usage { get; set; }
    public int Downloads { get; set; }
    public DateTime CreatedAt { get; set; }
}
