using Microsoft.AspNetCore.Components;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Resources;

public partial class ResourceExternalDependencyEditor
{
    [Parameter] public ExternalDependencyDTO[] Values { get; set; } = [];
    [Parameter] public EventCallback<ExternalDependencyDTO[]> ValuesChanged { get; set; }

    private Task AddAsync() => ValuesChanged.InvokeAsync(
    [
        .. Values,
        new ExternalDependencyDTO
        {
            DependencyName = string.Empty,
            DependencyURL = string.Empty
        },
    ]);

    private Task RemoveAsync(ExternalDependencyDTO item) => ValuesChanged.InvokeAsync(
        [.. Values.Where(value => !ReferenceEquals(value, item))]);
}
