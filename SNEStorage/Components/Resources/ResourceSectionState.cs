namespace SNEStorage.Components.Resources;

// UI state only; each controller request can complete or fail independently.
internal sealed class ResourceSectionState<T>
{
    public T? Data { get; set; }
    public bool Loading { get; set; } = true;
    public bool Started { get; set; }
    public int Revision { get; set; }
    public int? ErrorStatus { get; set; }
    public string? ErrorBody { get; set; }
    public bool Ready => Started && !Loading && ErrorStatus is null;
}
