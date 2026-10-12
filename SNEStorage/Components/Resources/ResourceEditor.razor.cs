using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using SNEStorage.DTOs;
using SNEStorage.Models;
using SNEStorage.Components.Shared;

namespace SNEStorage.Components.Resources;

public partial class ResourceEditor : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] ArchiveExtensions = [".zip", ".rar", ".7z", ".tar", ".gz"];
    private static readonly string[] TabTitles = ["General", "Previews", "Authorship", "Dependencies", "Content"];
    private readonly string _requestKey = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _lifetime = new();
    private IJSObjectReference? _module;
    private ResourceReadDTO? _resource;
    private ResourceVersion? _version;
    private ResourceEditorData _data = new();
    private ResourceCreateOptionsDto? _options;
    private EditorFields _form = new();
    private ResourceEditorSelectionSnapshot _originalSelections = new();
    private bool _saving;
    private bool _saved;
    private bool _focusInvalid;
    private int _activeTab;
    private bool _syncTabs;
    private bool _focusTab;
    private IReadOnlyDictionary<string, string> _authorNames = new Dictionary<string, string>();
    private SelectOptionDto[] _dependencyChoices = [];
    private ResourcePreview[] _existingPreviews = [];
    private bool _loading = true;
    private bool _loadPending = true;
    private bool _useAccessResource = true;
    private bool _disposed;
    private int? _errorStatus;
    private string? _errorBody;
    private string? _fileName;
    private string? _fileError;
    private string? _message;
    private string[] _validationErrors = [];

    [Parameter] public long ResourceId { get; set; }
    [Parameter] public long? VersionId { get; set; }
    [Parameter] public ResourceEditorMode Mode { get; set; }
    [CascadingParameter(Name = "VerifiedPageAccount")] private AccountPageContext? PageAccount { get; set; }
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private ILogger<ResourceEditor> Logger { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    protected string Title => Mode switch
    {
        ResourceEditorMode.CreateVersion => "Add version", ResourceEditorMode.EditVersion => "Edit version", _ => "Edit resource"
    };
    protected SelectOptionDto? InitialGame => _resource?.Videogame is { } game ? new() { Id = game.Id, Name = game.Name } : null;
    protected string FormId => $"resource-editor-{_requestKey}";
    protected string PreviewInputId => $"resource-edit-previews-{_requestKey}";
    private string TabId(int index) => $"{FormId}-tab-{index}";
    private string PanelId(int index) => $"{FormId}-panel-{index}";
    private void SelectTab(int index)
    {
        if (_saving || index < 0 || index >= TabTitles.Length) return;
        _activeTab = index;
        _syncTabs = true;
    }
    private void TabKeyDown(int index, KeyboardEventArgs args)
    {
        var next = args.Key switch
        {
            "ArrowRight" => (index + 1) % TabTitles.Length,
            "ArrowLeft" => (index + TabTitles.Length - 1) % TabTitles.Length,
            "Home" => 0, "End" => TabTitles.Length - 1, _ => -1
        };
        if (next < 0) return;
        SelectTab(next);
        _focusTab = true;
    }
    protected string SavedTitle => Mode == ResourceEditorMode.CreateVersion ? "Version uploaded successfully" : "Changes saved successfully";
    protected string SubmitLabel => _saving ? Mode == ResourceEditorMode.CreateVersion ? "Uploading…" : "Saving…"
        : Mode == ResourceEditorMode.CreateVersion ? "Upload version" : "Save changes";

    protected async Task SaveAsync(EditContext context)
    {
        if (_module is null || _resource is null || _saving || _saved || _disposed) return;
        _saving = true;
        _message = null;
        _validationErrors = [];
        try
        {
            var response = await _module.InvokeAsync<BrowserResponse>("submitEditor", _lifetime.Token,
                FormId, ResourceId, VersionId, Mode.ToString(), _originalSelections, _requestKey, PreviewInputId);
            if (_disposed) return;
            if (!string.IsNullOrWhiteSpace(response.ValidationMessage))
            {
                _message = response.ValidationMessage;
                if (Mode == ResourceEditorMode.Resource && response.ValidationTab is { } tab)
                {
                    _activeTab = Math.Clamp(tab, 0, TabTitles.Length - 1);
                    _syncTabs = true;
                }
                _focusInvalid = true;
                return;
            }
            if (response.Cancelled) { _message = "The request was interrupted. Your changes have not been confirmed."; return; }
            if (response.Status is 401 or 403) { _errorStatus = 403; _errorBody = response.Body; return; }
            if (response.Status is >= 200 and < 300)
            {
                if (!ResourceEditorResponse.ConfirmsSave(response.Body, Mode, ResourceId))
                {
                    _message = "The server did not confirm the saved changes. Check the resource before submitting again.";
                    return;
                }
                _saved = true;
                StateHasChanged();
                await Task.Delay(TimeSpan.FromSeconds(3), _lifetime.Token);
                if (!_disposed) Navigation.NavigateTo($"/Resource/{ResourceId}");
                return;
            }
            _message = response.Status == 0
                ? "The save response could not be received. Check the resource before submitting again."
                : ApiErrorPresentation.Describe(response.Status, response.Body,
                    Mode == ResourceEditorMode.EditVersion ? "resource" : "resource-upload").Message;
            _validationErrors = ResourceEditorResponse.ValidationErrors(response.Body);
        }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) when (_disposed) { }
        catch (JSException exception)
        {
            if (!_disposed)
            {
                Logger.LogWarning(exception, "The resource editor did not receive a save response.");
                _message = "The save response could not be received. Check the resource before submitting again.";
            }
        }
        finally { _saving = false; }
    }
    protected void Reload() { _loadPending = true; _useAccessResource = false; _loading = true; _errorStatus = null; _errorBody = null; }
    protected void FileSelected(InputFileChangeEventArgs args)
    {
        var file = args.File;
        _fileName = file.Name;
        _fileError = file.Size == 0 ? "Choose a nonempty archive."
            : file.Size > 250L * 1024 * 1024 ? "The archive must be no larger than 250 MB."
            : !ArchiveExtensions.Any(extension => file.Name.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                ? "Choose a ZIP, RAR, 7Z, TAR, GZ or TAR.GZ archive." : null;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed) return;
        if (_syncTabs && !_saving && _module is not null)
        {
            _syncTabs = false;
            var focus = _focusTab;
            _focusTab = false;
            try { await _module.InvokeVoidAsync("syncEditorTabs", _lifetime.Token, FormId, _activeTab, focus); }
            catch (JSDisconnectedException) { }
            catch (OperationCanceledException) when (_disposed) { }
            catch (JSException exception) { Logger.LogDebug(exception, "The editor tab could not be focused."); }
        }
        if (_focusInvalid && !_saving && _module is not null)
        {
            _focusInvalid = false;
            try { await _module.InvokeVoidAsync("focusInvalidEditor", _lifetime.Token, FormId); }
            catch (JSDisconnectedException) { }
            catch (OperationCanceledException) when (_disposed) { }
            catch (JSException exception) { Logger.LogDebug(exception, "The invalid editor field could not be focused."); }
        }
        if (!_loadPending) return;
        _loadPending = false;
        try
        {
            if (_disposed) return;
            if (ResourceId <= 0) { _errorStatus = 404; return; }
            _module ??= await JavaScript.InvokeAsync<IJSObjectReference>("import", _lifetime.Token, "./js/resource-editor.js?v=16");
            if (_disposed) return;
            var userId = PageAccount?.Profile.UserId;
            if (string.IsNullOrWhiteSpace(userId)) { _errorStatus = 403; return; }
            var reuseAccess = _useAccessResource && PageAccount?.Resource?.Id == ResourceId;
            _resource = reuseAccess ? PageAccount?.Resource : null;
            if (_resource is null)
            {
                var json = await _module.InvokeAsync<string>("getResource", _lifetime.Token, ResourceId, _requestKey);
                if (_disposed) return;
                var response = JsonSerializer.Deserialize<BrowserResponse>(json, JsonOptions);
                if (!AcceptResponse(response)) return;
                _resource = JsonSerializer.Deserialize<ResourceReadDTO>(response!.Body!, JsonOptions);
            }
            _useAccessResource = false;
            if (_resource is null || _resource.Id != ResourceId) { _errorStatus = 502; return; }
            if (!ResourceManagementAccess.CanDisplay(_resource, userId)) { _errorStatus = 403; return; }
            var dataLoad = LoadEditorDataAsync();
            var optionsLoad = Mode == ResourceEditorMode.Resource ? LoadOptionsAsync() : Task.FromResult(true);
            await Task.WhenAll(dataLoad, optionsLoad);
            if (_disposed || !await dataLoad || !await optionsLoad) return;
            if (Mode == ResourceEditorMode.EditVersion)
            {
                _version = _data.Versions.FirstOrDefault(version => version.Id == VersionId);
                if (_version is null) { _errorStatus = 404; return; }
            }
            _originalSelections = ResourceEditorSelectionSnapshot.From(_resource, _version, _data, userId);
            _authorNames = _data.Authors.GroupBy(author => author.UserSummary.Id)
                .ToDictionary(group => group.Key, group => string.IsNullOrWhiteSpace(group.First().UserSummary.Username)
                    ? "Unknown user" : group.First().UserSummary.Username);
            _dependencyChoices = _data.Dependencies.Select(dependency => new SelectOptionDto
            {
                Id = dependency.DependencyResourceId, Name = dependency.Name ?? $"Resource #{dependency.DependencyResourceId}"
            }).DistinctBy(dependency => dependency.Id).ToArray();
            _existingPreviews = _data.Previews.ToArray();
            _form = new()
            {
                Name = _resource.Name, Description = _resource.Description, VideogameId = _resource.Videogame.Id,
                VisibilityId = _resource.Visibility.Id,
                Types = _resource.Types.Select(type => type.ResourceTypeId).Distinct().ToArray(),
                Tags = _resource.Tags.Where(tag => !string.IsNullOrWhiteSpace(tag.Name))
                    .Select(tag => tag.Name).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                IAmAuthor = _data.Authors.Any(author => author.UserSummary.Id == userId),
                AuthorsIDs = _data.Authors.Select(author => author.UserSummary.Id)
                    .Where(id => id != userId).Distinct().ToArray(),
                ExternalAuthors = _data.ExternalAuthors.Select(author => author.Author).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                DependencyIDs = _data.Dependencies.Select(dependency => dependency.DependencyResourceId).Distinct().ToArray(),
                PreviewIDs = _existingPreviews.Select(preview => preview.Id).ToArray(),
                ExternalDependencies = _data.ExternalDependencies.Select(dependency => new ExternalDependencyDTO
                {
                    DependencyName = dependency.DependencyName, DependencyURL = dependency.DependencyURL,
                    Mirror1 = dependency.Mirror1, Mirror2 = dependency.Mirror2, Mirror3 = dependency.Mirror3, Mirror4 = dependency.Mirror4
                }).ToArray(),
                IncludesPorn = _resource.IncludesPorn, IncludesGore = _resource.IncludesGore,
                IncludesPolitics = _resource.IncludesPolitics, IncludesSlurs = _resource.IncludesSlurs,
                IncludesSensitiveContent = _resource.IncludesSensitiveContent,
                RepositoryURL = _resource.RepositoryURL, SealOfQualityRequested = _version?.SealOfQualityRequested ?? false,
                Version = Mode == ResourceEditorMode.CreateVersion ? "1.0.0" : _version?.Version,
                ChangeLog = _version?.ChangeLog
            };
        }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception) when (exception is JSException or JsonException or OperationCanceledException)
        {
            if (!_disposed) { Logger.LogWarning(exception, "The resource editor could not be loaded."); _errorStatus = exception is JsonException ? 502 : 0; }
        }
        finally
        {
            if (!_disposed) { _loading = false; StateHasChanged(); }
        }
    }
    private bool AcceptResponse(BrowserResponse? response)
    {
        if (response is { Cancelled: false, Status: >= 200 and < 300 } && !string.IsNullOrWhiteSpace(response.Body)) return true;
        _errorStatus = response is null ? 502 : response.Cancelled ? 499 : response.Status;
        _errorBody = response?.Body;
        return false;
    }
    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        _lifetime.Cancel();
        if (_module is not null)
        {
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _module.InvokeVoidAsync("cancelEditorRequest", cleanup.Token, _requestKey);
                await _module.DisposeAsync();
            }
            catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
        }
        _lifetime.Dispose();
    }
    private sealed class BrowserResponse
    {
        public int Status { get; set; }
        public string? Body { get; set; }
        public bool Cancelled { get; set; }
        public string? ValidationMessage { get; set; }
        public int? ValidationTab { get; set; }
    }
    private sealed class EditorFields
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public long? VideogameId { get; set; }
        public long VisibilityId { get; set; }
        public long[] Types { get; set; } = [];
        public string[] Tags { get; set; } = [];
        public string[] AuthorsIDs { get; set; } = [];
        public bool IAmAuthor { get; set; }
        public string[] ExternalAuthors { get; set; } = [];
        public long[] DependencyIDs { get; set; } = [];
        public ExternalDependencyDTO[] ExternalDependencies { get; set; } = [];
        public long[] PreviewIDs { get; set; } = [];
        public bool IncludesPorn { get; set; }
        public bool IncludesGore { get; set; }
        public bool IncludesPolitics { get; set; }
        public bool IncludesSlurs { get; set; }
        public bool IncludesSensitiveContent { get; set; }
        public string? RepositoryURL { get; set; }
        public bool SealOfQualityRequested { get; set; }
        public string? Version { get; set; }
        public string? ChangeLog { get; set; }
    }
}
