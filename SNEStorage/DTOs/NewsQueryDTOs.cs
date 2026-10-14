using System.ComponentModel.DataAnnotations;

namespace SNEStorage.DTOs;

public class NewsFilterDTO { }
public class NewsReadDTO
{
    public long Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public required string Title { get; set; }
    public required string Text { get; set; }
    public required UserNameDTO UserSummary { get; set; }
}
public class NewsCreateDTO
{
    [Required, StringLength(200)]
    public required string Title { get; set; }

    [Required]
    public required string Text { get; set; }
}
public class NewsEditDTO
{
    [StringLength(200)]
    public string? Title { get; set; }
    public string? Text { get; set; }
}
