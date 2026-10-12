namespace SNEStorage.DTOs;

public sealed class ResourceCreateOptionsDto
{
    public IReadOnlyList<ResourceTypeDTO> ResourceTypes { get; set; } = [];
    public IReadOnlyList<IdNamePair> Visibilities { get; set; } = [];
}
