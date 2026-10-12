namespace SNEStorage.DTOs;

public sealed class RegistrationOptionsDto
{
    public IReadOnlyList<IdNamePair> TimeZones { get; set; } = [];
    public IReadOnlyList<IdNamePair> EmailVisibilities { get; set; } = [];
}
