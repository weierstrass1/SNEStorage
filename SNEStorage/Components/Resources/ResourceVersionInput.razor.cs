using Microsoft.AspNetCore.Components;

namespace SNEStorage.Components.Resources;

// Presentation only: combines the visible controls into the DTO's single Version field.
public partial class ResourceVersionInput
{
    [Parameter, EditorRequired] public string InputId { get; set; } = string.Empty;
    [Parameter] public string? Value { get; set; }
    [Parameter] public EventCallback<string> ValueChanged { get; set; }

    private string? _lastValue;
    private string _baseVersion = string.Empty;
    private string _release = string.Empty;
    private string _releaseNumber = string.Empty;

    private string CompleteVersion => _baseVersion.Trim() + (_release.Length == 0 ? string.Empty
        : "-" + _release + (string.IsNullOrWhiteSpace(_releaseNumber) ? string.Empty : "." + _releaseNumber.Trim()));

    protected override void OnParametersSet()
    {
        var value = Value ?? string.Empty;
        if (value == _lastValue) return;
        _lastValue = value;
        _baseVersion = value;
        _release = _releaseNumber = string.Empty;
        var parts = value.Split('-', 2);
        if (parts.Length != 2) return;
        var suffix = parts[1].Split('.', 2);
        if (suffix[0] is not ("alpha" or "beta" or "rc")) return;
        if (suffix.Length == 2 && suffix[1].Length == 0) return;
        _baseVersion = parts[0];
        _release = suffix[0];
        _releaseNumber = suffix.Length == 2 ? suffix[1] : string.Empty;
    }

    private Task BaseChangedAsync(ChangeEventArgs args)
    {
        _baseVersion = args.Value?.ToString() ?? string.Empty;
        return PublishAsync();
    }

    private Task ReleaseChangedAsync(ChangeEventArgs args)
    {
        _release = args.Value?.ToString() ?? string.Empty;
        return PublishAsync();
    }

    private Task NumberChangedAsync(ChangeEventArgs args)
    {
        _releaseNumber = args.Value?.ToString() ?? string.Empty;
        return PublishAsync();
    }

    private Task PublishAsync()
    {
        _lastValue = CompleteVersion;
        return ValueChanged.InvokeAsync(_lastValue);
    }
}
