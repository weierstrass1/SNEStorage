using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Resources;

public partial class ResourceTokenInput
{
    private string _pending = string.Empty;
    private string? _error;

    [Parameter, EditorRequired]
    public string InputId { get; set; } = string.Empty;

    [Parameter]
    public string FieldName { get; set; } = "Tags";

    [Parameter]
    public string[] Values { get; set; } = [];

    [Parameter]
    public EventCallback<string[]> ValuesChanged { get; set; }

    protected Task TextChangedAsync()
    {
        var parts = _pending.Split(',');
        _error = parts.Any(tag => tag.Trim().Length > TextLimits.TAG)
            ? $"Each tag can contain up to {TextLimits.TAG} characters." : null;
        if (_error is not null) return Task.CompletedTask;
        if (parts.Length > 1)
        {
            _pending = parts[^1];
            return AppendAsync(parts.Take(parts.Length - 1));
        }
        return Task.CompletedTask;
    }

    protected Task KeyDownAsync(KeyboardEventArgs args) => args.Key == "Enter" ? AddPendingAsync() : Task.CompletedTask;

    protected async Task AddPendingAsync()
    {
        if (_pending.Split(',').Any(tag => tag.Trim().Length > TextLimits.TAG))
        {
            _error = $"Each tag can contain up to {TextLimits.TAG} characters.";
            return;
        }
        _error = null;
        var pending = _pending;
        _pending = string.Empty;
        await AppendAsync(pending.Split(','));
    }

    protected Task RemoveAsync(string value) => ValuesChanged.InvokeAsync([.. Values.Where(tag => tag != value)]);

    private Task AppendAsync(IEnumerable<string> tags) => ValuesChanged.InvokeAsync(
        [.. Values.Concat(tags.Select(tag => tag.Trim()).Where(tag => tag.Length > 0)).Distinct(StringComparer.OrdinalIgnoreCase)]);
}
