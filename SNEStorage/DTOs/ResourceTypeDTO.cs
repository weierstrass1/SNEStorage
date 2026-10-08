namespace SNEStorage.DTOs;
public class ResourceTypeDTO
{
    public required long Id { get; set; }
    public required string Name { get; set; }
    public required long CategoryId { get; set; }
    public required string CategoryName { get; set; }
}
