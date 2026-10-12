namespace SNEStorage.Components.Shared;

// Frontend route metadata. Controllers and services remain responsible for API authorization.
[AttributeUsage(AttributeTargets.Class)]
public sealed class RequireAccountAttribute : Attribute
{
    public bool ResourceManagement { get; init; }
    public bool StaffOnly { get; init; }
}
