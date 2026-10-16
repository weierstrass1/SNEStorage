using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using SNEStorage.DTOs;
using SNEStorage.Components.Shared;

namespace SNEStorage.Components.Pages;

[RequireAccount]
public partial class CreateResource : IAsyncDisposable
{
    private const string FormId = "resource-create-form";
    private const long MaxArchiveBytes = 250L * 1024 * 1024;
    private static readonly string[] TabTitles = ["General", "File and release", "Previews", "Authorship", "Dependencies", "Content"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] ArchiveExtensions = [".zip", ".rar", ".7z", ".tar", ".gz", ".tar.gz"];
    // Incomplete inputs stay in UI state; the browser submits the DTO fields and files as multipart data.
    private readonly ResourceSubmissionFields _form = new();
    private ResourceCreateOptionsDto? _options;
    private IJSObjectReference? _module;
    private readonly List<string> _validationErrors = [];
    private bool _forbidden;
    private bool _loading = true;
    private bool _saving;
    private bool _disposed;
    private string? _error;
    private string? _fileName;
    private string? _fileSize;
    private string? _fileError;
    private string? _currentUserId;
    private int _activeTab;
    private bool _focusInvalid;
    private bool _tabChanged;
    protected string LoginUrl => LoginNavigation.GetLoginUrl(Navigation);

    protected void SelectTab(int tab)
    {
        _activeTab = Math.Clamp(tab, 0, TabTitles.Length - 1);
        _tabChanged = true;
    }

    [CascadingParameter(Name = "VerifiedPageAccount")]
    private AccountPageContext? PageAccount { get; set; }

    [Inject]
    private IJSRuntime JavaScript { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private ILogger<CreateResource> Logger { get; set; } = default!;

    protected override void OnParametersSet()
    {
        _currentUserId = PageAccount?.Profile.UserId;
        _forbidden = string.IsNullOrWhiteSpace(_currentUserId);
        if (_forbidden) _loading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_forbidden || _disposed)
        {
            return;
        }

        if (!firstRender)
        {
            if (_module is not null && (_focusInvalid || _tabChanged))
            {
                var focusInvalid = _focusInvalid;
                _focusInvalid = false;
                _tabChanged = false;
                try
                {
                    await _module.InvokeVoidAsync("syncTabs", FormId, $"resource-tab-{_activeTab}");
                    if (focusInvalid) await _module.InvokeVoidAsync("focusInvalidField", FormId);
                }
                catch (JSDisconnectedException) { }
            }
            return;
        }

        try
        {
            var initial = await BrowserReadStartup.LoadAsync<BrowserResponse>(JavaScript,
                "./js/resource-create.js?v=19", "getCreateOptions", [], FormId);
            if (_disposed) { if (initial.Module is not null) await initial.Module.DisposeAsync(); return; }
            if (initial.Cancelled || initial.Module is null || initial.Response is null) return;
            _module = initial.Module;
            await ReadOptionsAsync(initial.Response);
        }
        catch (JSException exception)
        {
            Logger.LogWarning(exception, "Resource submission could not be initialized.");
            _error = "The submission form could not be loaded. Please reload the page.";
            _loading = false;
        }

        if (!_disposed)
        {
            StateHasChanged();
        }
    }

    protected Task LoadOptionsAsync() => ReadOptionsAsync();

    private async Task ReadOptionsAsync(BrowserResponse? initial = null)
    {
        if (_module is null || _disposed || _forbidden)
        {
            return;
        }

        _loading = true;
        _error = null;
        try
        {
            var response = initial ?? await _module.InvokeAsync<BrowserResponse>("getCreateOptions");
            if (_disposed)
            {
                return;
            }

            if (response.Status is 401 or 403)
            {
                _forbidden = true;
                return;
            }

            if (response.Status != 200 || !response.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
            {
                _error = ApiErrorPresentation.Describe(response.Status, response.Body, "resource").Message;
                return;
            }

            _options = JsonSerializer.Deserialize<ResourceCreateOptionsDto>(response.Body, JsonOptions);
            if (_options is null || _options.ResourceTypes.Count == 0 || _options.Visibilities.Count == 0)
            {
                _options = null;
                _error = "Some submission options are unavailable. Please try again later.";
                return;
            }

        }
        catch (Exception exception) when (exception is JSException or JsonException)
        {
            Logger.LogWarning(exception, "Resource creation options could not be loaded.");
            _error = "Resource options could not be loaded. Please try again later.";
        }
        finally
        {
            _loading = false;
        }
    }

    protected void FileSelected(InputFileChangeEventArgs args)
    {
        if (args.FileCount == 0)
        {
            _fileName = null;
            _fileSize = null;
            _fileError = null;
            return;
        }

        var file = args.File;
        _fileName = file.Name;
        _fileSize = file.Size >= 1024 * 1024
            ? $"{file.Size / (1024d * 1024d):0.##} MB"
            : $"{file.Size / 1024d:0.##} KB";
        _fileError = file.Size == 0
            ? "Choose an archive that is not empty."
            : file.Size > MaxArchiveBytes
                ? "The archive must be no larger than 250 MB."
                : ArchiveExtensions.Any(extension => file.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    ? null
                    : "Choose a ZIP, RAR, 7Z, TAR, GZ, or TAR.GZ archive.";
    }

    protected async Task SubmitAsync(EditContext context)
    {
        if (_module is null || _saving || _forbidden || _disposed)
        {
            return;
        }

        _saving = true;
        StateHasChanged();
        try
        {
            await _module.InvokeVoidAsync("beginSubmission", FormId);
            if (_disposed) return;
            await ValidateAndSubmitAsync();
        }
        catch (JSDisconnectedException) { }
        catch (JSException exception)
        {
            Logger.LogWarning(exception, "Resource submission could not be started.");
            _error = "The submission could not start. Your form has been kept. Please try again.";
        }
        finally
        {
            if (!_disposed && _module is not null)
            {
                try
                {
                    using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                    await _module.InvokeVoidAsync("endSubmission", cleanup.Token, FormId);
                }
                catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
            }
            _saving = false;
        }
    }

    private async Task ValidateAndSubmitAsync()
    {
        _error = null;
        _validationErrors.Clear();
        try
        {
            var validation = await _module!.InvokeAsync<FormValidation>("validateResourceForm", FormId);
            if (_disposed) return;
            if (!validation.Valid)
            {
                SelectTab(validation.Tab);
                _error = validation.Message;
                _focusInvalid = true;
                return;
            }
        }
        catch (JSException exception)
        {
            Logger.LogWarning(exception, "Resource form validation could not be completed.");
            _error = "The form could not be checked. Please reload the page and try again.";
            return;
        }
        if (_form.VideogameId is null || _form.VisibilityId == 0 || _form.Types.Length == 0)
        {
            SelectTab(0);
            _error = "Choose a video game, at least one resource type, and a visibility setting.";
            return;
        }
        if (_fileError is not null)
        {
            SelectTab(1);
            _error = _fileError;
            return;
        }

        try
        {
            var response = await _module!.InvokeAsync<BrowserResponse>("submitResource", FormId);
            if (_disposed)
            {
                return;
            }

            if (response.Status is >= 200 and < 300 && response.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase)
                && TryReadResourceId(response.Body, out var resourceId))
            {
                Navigation.NavigateTo($"/Resource/Created/{resourceId}");
                return;
            }

            _forbidden = response.Status is 401 or 403;
            _error = response.Status is >= 200 and < 300
                ? "The server did not return a resource ID. Check your resources before submitting again."
                : ApiErrorPresentation.Describe(response.Status, response.Body, "resource-upload").Message;
            ReadValidationErrors(response);
        }
        catch (JSException exception)
        {
            Logger.LogWarning(exception, "Resource submission did not return a response.");
            _error = "The upload response could not be received. Check your resources before submitting again.";
        }
    }

    private static bool TryReadResourceId(string body, out long resourceId)
    {
        resourceId = 0;
        try
        {
            using var document = JsonDocument.Parse(body);
            var value = document.RootElement;
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("id", out var id)) value = id;
            return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out resourceId) && resourceId > 0;
        }
        catch (JsonException) { return false; }
    }

    private void ReadValidationErrors(BrowserResponse response)
    {
        if (response.Status is not (400 or 422) || !response.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            using var body = JsonDocument.Parse(response.Body);
            if (body.RootElement.ValueKind == JsonValueKind.Object && body.RootElement.TryGetProperty("errors", out var errors)
                && errors.ValueKind == JsonValueKind.Object)
            {
                var firstField = string.Empty;
                foreach (var field in errors.EnumerateObject())
                {
                    if (firstField.Length == 0) firstField = field.Name;
                    if (field.Value.ValueKind == JsonValueKind.Array)
                    {
                        _validationErrors.AddRange(field.Value.EnumerateArray()
                            .Where(value => value.ValueKind == JsonValueKind.String)
                            .Select(value => value.GetString()!));
                    }
                }
                if (firstField.Length > 0) SelectTab(firstField switch
                {
                    var name when name.StartsWith("Includes", StringComparison.OrdinalIgnoreCase) => 5,
                    var name when name.StartsWith("Previews", StringComparison.OrdinalIgnoreCase) => 2,
                    var name when name.StartsWith("DependencyIDs", StringComparison.OrdinalIgnoreCase)
                        || name.StartsWith("ExternalDependencies", StringComparison.OrdinalIgnoreCase) => 4,
                    "File" or "Version" or "ChangeLog" => 1,
                    "IAmAuthor" or "AuthorsIDs" or "ExternalAuthors" => 3,
                    _ => 0
                });
            }
        }
        catch (JsonException)
        {
            // Keep the status-based message when the error body is not a validation response.
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        try
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
            await BrowserReadStartup.CancelAsync(JavaScript, FormId, cleanup.Token);
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("cancelSubmission", FormId);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
                // The browser connection has already closed.
            }
        }
    }

    private sealed class BrowserResponse
    {
        public int Status { get; set; }
        public string Body { get; set; } = string.Empty;
        public string ContentType { get; set; } = string.Empty;
    }

    private sealed class FormValidation
    {
        public bool Valid { get; set; }
        public int Tab { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
