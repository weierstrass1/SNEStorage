using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Resources;

public partial class ResourceDeleteButton : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _requestKey = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _lifetime = new();
    private string _headingId => "delete-heading-" + _requestKey;
    private string _descriptionId => "delete-reason-" + _requestKey;
    private string _confirmationId => "delete-confirmation-" + _requestKey;
    private ElementReference _dialog;
    private IJSObjectReference? _module;
    private bool _disposed, _opening, _saving, _checking, _uncertain;
    private ErrorCopy? _error;
    private string? _openError;
    private ServiceResultTypes? _serverBlockReason;
    private long _lastResourceId;
    private long? _lastVersionId;
    private bool _lastCanDelete;
    private ServiceResultTypes? _lastBlockReason;

    [Parameter, EditorRequired] public long ResourceId { get; set; }
    [Parameter] public string ResourceName { get; set; } = "Resource";
    [Parameter] public long? VersionId { get; set; }
    [Parameter] public string? VersionName { get; set; }
    [Parameter] public string? CurrentUserId { get; set; }
    [Parameter] public bool CanDelete { get; set; }
    [Parameter] public ServiceResultTypes? BlockReason { get; set; }
    [Parameter] public bool IsStaff { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public EventCallback<bool> BusyChanged { get; set; }
    [Parameter] public EventCallback OnDeleted { get; set; }
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;

    private bool Allowed => !_disposed && CanDelete && _serverBlockReason is null && !string.IsNullOrWhiteSpace(CurrentUserId);
    private bool IsVersion => VersionId is not null;
    private string TargetLabel => IsVersion ? "version" : "resource";
    private string? DisabledDescription => Allowed ? null : (_serverBlockReason ?? BlockReason) switch
    {
        ServiceResultTypes.CantDeleteOnlyVersion => "This version cannot be deleted because a resource must keep at least one version. This restriction also applies to staff.",
        ServiceResultTypes.VersionHasSealOfQuality => "This version cannot be deleted because it has the Seal of Quality. Contact a moderator or administrator if you need it removed.",
        ServiceResultTypes.ResourceHasSealOfQuality => "This resource cannot be deleted because one of its versions has the Seal of Quality. Contact a moderator or administrator if you need it removed.",
        ServiceResultTypes.ResourceHasDependents => "This resource cannot be deleted because other resources depend on it. Contact a moderator or administrator if you need it removed.",
        _ => $"This {TargetLabel} cannot be deleted with your current permissions. Contact a moderator or administrator if you need it removed."
    };

    protected override void OnParametersSet()
    {
        if (_lastResourceId == ResourceId && _lastVersionId == VersionId && _lastCanDelete == CanDelete && _lastBlockReason == BlockReason) return;
        _lastResourceId = ResourceId;
        _lastVersionId = VersionId;
        _lastCanDelete = CanDelete;
        _lastBlockReason = BlockReason;
        _serverBlockReason = null;
    }

    private async Task OpenAsync()
    {
        if (!Allowed || Disabled || _opening || _saving) return;
        _opening = true;
        _openError = null;
        if (!_uncertain) _error = null;
        try
        {
            _module ??= await JavaScript.InvokeAsync<IJSObjectReference>("import", _lifetime.Token, "./js/resource-deletion.js?v=2");
            if (!_disposed) await _module.InvokeVoidAsync("openDialog", _lifetime.Token, _dialog);
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException)
        {
            if (!_disposed) _openError = "The confirmation could not open. Reload this page and try again.";
        }
        finally { _opening = false; }
    }

    private async Task CloseAsync()
    {
        if (_saving || _checking || _disposed || _module is null) return;
        try { await _module.InvokeVoidAsync("closeDialog", _lifetime.Token, _dialog); }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
    }

    private async Task DeleteAsync()
    {
        if (!Allowed || Disabled || _saving || _uncertain || _module is null) return;
        _saving = true;
        _error = null;
        try
        {
            await BusyChanged.InvokeAsync(true);
            if (_disposed) return;
            var json = await _module.InvokeAsync<string>("deleteResource", _lifetime.Token, ResourceId, CurrentUserId, _requestKey, VersionId);
            if (_disposed) return;
            var response = JsonSerializer.Deserialize<DeleteResponse>(json, JsonOptions);
            if (response is { Status: 204 } || response is { Status: >= 200 and < 300 } && ApiErrorPresentation.IsSuccess(response.Body, "resource"))
            {
                await DeletedAsync();
                return;
            }
            var code = ReadCode(response?.Body);
            if (response?.Status == 404 && (code == nameof(ServiceResultTypes.ResourceNotFound) || IsVersion && code == nameof(ServiceResultTypes.VersionNotFound)))
            {
                await DeletedAsync();
                return;
            }
            if (code is nameof(ServiceResultTypes.ResourceHasSealOfQuality) or nameof(ServiceResultTypes.ResourceHasDependents) or nameof(ServiceResultTypes.VersionHasSealOfQuality) or nameof(ServiceResultTypes.CantDeleteOnlyVersion))
                _serverBlockReason = Enum.Parse<ServiceResultTypes>(code);
            if (code == "AccountChanged")
                _error = new("Account changed", "Reload this page to check permissions for your current account.");
            else if (code.StartsWith("Captcha", StringComparison.Ordinal) || code == "AntiforgeryTokenMissing")
                _error = ApiErrorPresentation.Describe(response?.Status ?? 0, response?.Body, "resource");
            else if (response is null || response.Status is 0 or 408 or 499 or (>= 500) or (>= 200 and < 300))
                SetUncertain();
            else _error = ApiErrorPresentation.Describe(response.Status, response.Body, "resource");
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or JsonException or OperationCanceledException)
        {
            if (!_disposed) SetUncertain();
        }
        finally { _saving = false; if (!_disposed) await BusyChanged.InvokeAsync(false); }
    }

    private void SetUncertain()
    {
        _uncertain = true;
        _error = new("Deletion not confirmed", $"We could not confirm whether the {TargetLabel} was deleted. Check its status before trying again.");
    }

    private async Task CheckStatusAsync()
    {
        if (_checking || _disposed || _module is null) return;
        _checking = true;
        try
        {
            var json = await _module.InvokeAsync<string>("checkResource", _lifetime.Token, ResourceId, CurrentUserId, _requestKey, VersionId);
            if (_disposed) return;
            var response = JsonSerializer.Deserialize<DeleteResponse>(json, JsonOptions);
            if (response is { Status: 404 } && ReadCode(response.Body) == nameof(ServiceResultTypes.ResourceNotFound))
                await DeletedAsync();
            else if (IsVersion && response is { Status: 200 })
            {
                var versions = JsonSerializer.Deserialize<List<Models.ResourceVersion>>(response.Body ?? "", JsonOptions)
                    ?? throw new JsonException("Version list missing.");
                var version = versions.FirstOrDefault(item => item.Id == VersionId);
                if (version is null)
                    await DeletedAsync();
                else
                {
                    _uncertain = false;
                    _serverBlockReason = versions.Count <= 1 ? ServiceResultTypes.CantDeleteOnlyVersion :
                        version.SealOfQuality && !IsStaff ? ServiceResultTypes.VersionHasSealOfQuality : null;
                    _error = new("Version still exists", _serverBlockReason is null ? "The version still exists. You can try deleting it again." : DisabledDescription!);
                }
            }
            else if (response is { Status: 200 } && JsonSerializer.Deserialize<ResourceReadDTO>(response.Body ?? "", JsonOptions) is { } resource && resource.Id == ResourceId)
            {
                _uncertain = false;
                _serverBlockReason = resource.CanDelete ? null : resource.DeleteBlockReason ?? ServiceResultTypes.Unauthorized;
                _error = new("Resource still exists", resource.CanDelete ? "The resource still exists. You can try deleting it again." : DisabledDescription ?? "Your permissions have changed. Reload the page.");
            }
            else _error = new("Status unavailable", $"The {TargetLabel} status could not be confirmed. Reload the page before attempting another deletion.");
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or JsonException or OperationCanceledException)
        {
            if (!_disposed) _error = new("Status unavailable", $"The {TargetLabel} status could not be confirmed. Try checking again later.");
        }
        finally { _checking = false; }
    }

    private async Task DeletedAsync()
    {
        if (_module is not null)
        {
            try { await _module.InvokeVoidAsync("closeDialog", _lifetime.Token, _dialog); }
            catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
        }
        await OnDeleted.InvokeAsync();
    }

    private static string ReadCode(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return "";
        string code;
        try
        {
            using var document = JsonDocument.Parse(body);
            code = document.RootElement.ValueKind == JsonValueKind.String ? document.RootElement.GetString() ?? "" :
                document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.String ? error.GetString() ?? "" : "";
        }
        catch (JsonException) { code = body.Trim(); }
        return code[(code.LastIndexOf('-') + 1)..];
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _lifetime.Cancel();
        if (_module is not null)
        {
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _module.InvokeVoidAsync("dispose", cleanup.Token, _requestKey, _dialog);
                await _module.DisposeAsync();
            }
            catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
        }
        _lifetime.Dispose();
    }

    private sealed class DeleteResponse
    {
        public int Status { get; set; }
        public string? Body { get; set; }
    }
}
