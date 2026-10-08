using Microsoft.AspNetCore.Components;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Pages;

public partial class Resources
{
    private List<ResourceSummaryDto> _resources = [];
    private bool _isLoading = true;
    private string? _error;

    [Inject]
    private HttpClient Http { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _resources = await Http.GetFromJsonAsync<List<ResourceSummaryDto>>("resources") ?? [];
        }
        catch (HttpRequestException)
        {
            _error = "Resources could not be loaded. Please try again later.";
        }
        finally
        {
            _isLoading = false;
        }
    }
}
