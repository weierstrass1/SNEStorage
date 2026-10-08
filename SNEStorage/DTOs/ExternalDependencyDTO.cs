namespace SNEStorage.DTOs;
public class ExternalDependencyDTO
{
    public required string DependencyName { get; set; }
    public required string DependencyURL { get; set; }
    public string? Mirror1 { get; set; }
    public string? Mirror2 { get; set; }
    public string? Mirror3 { get; set; }
    public string? Mirror4 { get; set; }
}
