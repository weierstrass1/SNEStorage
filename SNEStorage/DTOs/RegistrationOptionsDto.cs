namespace SNEStorage.DTOs;

public sealed class RegistrationOptionsDto
{
    public IReadOnlyList<SelectOptionDto> TimeZones { get; set; } = [];
    public IReadOnlyList<SelectOptionDto> EmailVisibilities { get; set; } = [];
}
