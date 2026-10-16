using System.Text.Json;
using Microsoft.JSInterop;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Components.Resources;

public partial class ResourceEditor
{
    private async Task<bool> LoadEditorDataAsync(IReadOnlyDictionary<string, string> initial)
    {
        Task<ResourceReadResult<T>> Read<T>(string section) where T : class =>
            Task.FromResult(initial.TryGetValue(section, out var json)
                ? ResourceApiClient.Decode<T>(json) : new ResourceReadResult<T>(502, null, null));
        if (Mode == ResourceEditorMode.CreateVersion)
        {
            _data = new();
            return true;
        }
        if (Mode == ResourceEditorMode.EditVersion)
        {
            var versions = await Read<List<ResourceVersion>>("versions");
            if (_disposed) return false;
            if (!AcceptSection(versions)) return false;
            _data = new() { Versions = versions.Data! };
            return true;
        }

        var authorsLoad = Read<List<ResourceAuthorDTO>>("authors");
        var externalAuthorsLoad = Read<List<ResourceExternalAuthor>>("external-authors");
        var dependenciesLoad = Read<List<ResourceDependencyViewDTO>>("dependencies");
        var externalDependenciesLoad = Read<List<ResourceExternalDependency>>("external-dependencies");
        var previewsLoad = Read<List<ResourcePreview>>("previews");
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

    private Task<bool> LoadOptionsAsync(string? json)
    {
        if (_disposed || json is null) return Task.FromResult(false);
        var response = JsonSerializer.Deserialize<BrowserResponse>(json, JsonOptions);
        if (!AcceptResponse(response)) return Task.FromResult(false);
        _options = JsonSerializer.Deserialize<ResourceCreateOptionsDto>(response!.Body!, JsonOptions);
        if (_options is not null) return Task.FromResult(true);
        _errorStatus = 502;
        return Task.FromResult(false);
    }
}
