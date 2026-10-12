using System.Text.Json;
using Microsoft.JSInterop;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Components.Resources;

public partial class ResourceEditor
{
    private async Task<bool> LoadEditorDataAsync()
    {
        var client = new ResourceApiClient(_module!, _requestKey, _lifetime.Token);
        if (Mode == ResourceEditorMode.CreateVersion)
        {
            _data = new();
            return true;
        }
        if (Mode == ResourceEditorMode.EditVersion)
        {
            var versions = await client.ReadSectionAsync<List<ResourceVersion>>(ResourceId, "versions");
            if (_disposed) return false;
            if (!AcceptSection(versions)) return false;
            _data = new() { Versions = versions.Data! };
            return true;
        }

        var authorsLoad = client.ReadSectionAsync<List<ResourceAuthorDTO>>(ResourceId, "authors");
        var externalAuthorsLoad = client.ReadSectionAsync<List<ResourceExternalAuthor>>(ResourceId, "external-authors");
        var dependenciesLoad = client.ReadSectionAsync<List<ResourceDependencyViewDTO>>(ResourceId, "dependencies");
        var externalDependenciesLoad = client.ReadSectionAsync<List<ResourceExternalDependency>>(ResourceId, "external-dependencies");
        var previewsLoad = client.ReadSectionAsync<List<ResourcePreview>>(ResourceId, "previews");
        await Task.WhenAll(authorsLoad, externalAuthorsLoad, dependenciesLoad, externalDependenciesLoad, previewsLoad);
        if (_disposed) return false;
        var authorsResult = await authorsLoad;
        var externalAuthorsResult = await externalAuthorsLoad;
        var dependenciesResult = await dependenciesLoad;
        var externalDependenciesResult = await externalDependenciesLoad;
        var previewsResult = await previewsLoad;
        if (!AcceptSection(authorsResult) || !AcceptSection(externalAuthorsResult) ||
            !AcceptSection(dependenciesResult) || !AcceptSection(externalDependenciesResult) || !AcceptSection(previewsResult)) return false;
        _data = new()
        {
            Authors = authorsResult.Data!, ExternalAuthors = externalAuthorsResult.Data!,
            Dependencies = dependenciesResult.Data!, ExternalDependencies = externalDependenciesResult.Data!, Previews = previewsResult.Data!
        };
        return true;
    }

    private bool AcceptSection<T>(ResourceReadResult<T> response) where T : class
    {
        if (response.Success) return true;
        _errorStatus = response.Status is 401 or 403 ? 403 : response.Status;
        _errorBody = response.Body;
        return false;
    }

    private async Task<bool> LoadOptionsAsync()
    {
        var json = await _module!.InvokeAsync<string>("getResourceOptions", _lifetime.Token, _requestKey);
        if (_disposed) return false;
        var response = JsonSerializer.Deserialize<BrowserResponse>(json, JsonOptions);
        if (!AcceptResponse(response)) return false;
        _options = JsonSerializer.Deserialize<ResourceCreateOptionsDto>(response!.Body!, JsonOptions);
        if (_options is not null) return true;
        _errorStatus = 502;
        return false;
    }
}
