using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace SNEStorage.Components.Resources;

public partial class ResourceTokenInput
{
    private string _pending = string.Empty;

    [Parameter, EditorRequired]
    public string InputId { get; set; } = string.Empty;

    [Parameter]
    public string FieldName { get; set; } = "Tags";

    [Parameter]
    public string[] Values { get; set; } = [];

    [Parameter]
    public EventCallback<string[]> ValuesChanged { get; set; }

    protected async Task TextChangedAsync(ChangeEventArgs args)
    {
        var parts = (args.Value?.ToString() ?? string.Empty).Split(',');
        _pending = parts[^1];
        if (parts.Length > 1)
        {
            await AppendAsync(parts.Take(parts.Length - 1));
        }
    }

    protected Task KeyDownAsync(KeyboardEventArgs args) => args.Key == "Enter" ? AddPendingAsync() : Task.CompletedTask;

    protected async Task AddPendingAsync()
    {
        var pending = _pending;
        _pending = string.Empty;
        await AppendAsync(pending.Split(','));
    }

    protected Task RemoveAsync(string value) => ValuesChanged.InvokeAsync([.. Values.Where(tag => tag != value)]);

    private Task AppendAsync(IEnumerable<string> tags) => ValuesChanged.InvokeAsync(
        [.. Values.Concat(tags.Select(tag => tag.Trim()).Where(tag => tag.Length > 0)).Distinct(StringComparer.OrdinalIgnoreCase)]);
}
