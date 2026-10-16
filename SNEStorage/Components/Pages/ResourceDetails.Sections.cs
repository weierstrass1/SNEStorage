using System.Text.Json;
using Microsoft.JSInterop;
using SNEStorage.Components.Resources;
using SNEStorage.DTOs;
using SNEStorage.Models;

namespace SNEStorage.Components.Pages;

public partial class ResourceDetails
{
    private ResourceSectionState<List<ResourceAuthorDTO>> _authors = new();
    private ResourceSectionState<List<ResourceExternalAuthor>> _externalAuthors = new();
    private ResourceSectionState<List<ResourceDependencyViewDTO>> _dependencies = new();
    private ResourceSectionState<List<ResourceExternalDependency>> _externalDependencies = new();
    private ResourceSectionState<ResourceVersion> _latest = new();
    private ResourceSectionState<RatingReadDto> _rating = new();
    private ResourceSectionState<List<ResourcePreview>> _previews = new();
    private ResourceSectionState<List<ResourceVersion>> _versions = new() { Loading = false };
    private bool _refreshingVersionDetails;
    private string? _versionDeletionNotice;

    private void ResetSections()
    {
        _authors = new();
        _externalAuthors = new();
        _dependencies = new();
        _externalDependencies = new();
        _latest = new();
        _rating = new();
        _previews = new();
        _versions = new() { Loading = false };
        _refreshingVersionDetails = false;
        _versionDeletionNotice = null;
    }

    private Task LoadOverviewSectionsAsync() => Task.WhenAll(
        LoadAuthorsAsync(), LoadExternalAuthorsAsync(), LoadDependenciesAsync(),
        LoadExternalDependenciesAsync(), LoadLatestAsync(), LoadRatingAsync(), LoadPreviewsAsync());

    private Task LoadAuthorsAsync() => LoadSectionAsync(_authors, "authors");
    private Task LoadExternalAuthorsAsync() => LoadSectionAsync(_externalAuthors, "external-authors");
    private Task LoadDependenciesAsync() => LoadSectionAsync(_dependencies, "dependencies");
    private Task LoadExternalDependenciesAsync() => LoadSectionAsync(_externalDependencies, "external-dependencies");
    private Task LoadLatestAsync() => LoadSectionAsync(_latest, "versions/latest");
    private Task LoadRatingAsync() => LoadSectionAsync(_rating, "rating");
    private Task LoadPreviewsAsync() => LoadSectionAsync(_previews, "previews");
    private Task LoadVersionsAsync() => LoadSectionAsync(_versions, "versions");

    private async Task RefreshVersionDetailsAsync()
    {
        if (_disposed || _module is null || _resource is null || _refreshingVersionDetails) return;
        var generation = _generation;
        _refreshingVersionDetails = true;
        _versionDeletionNotice = "Version removed. Updating resource details…";
        _versions = new() { Loading = false };
        _latest = new();
        var summaryTask = RefreshVersionSummaryAsync();
        await Task.WhenAll(LoadVersionsAsync(), LoadLatestAsync(), summaryTask);
        if (_disposed || generation != _generation) return;
        _refreshingVersionDetails = false;
        _versionDeletionNotice = _versions.Ready && _latest.Ready && await summaryTask ?
            "The version was removed. Resource details are up to date." :
            "The version was removed, but some resource details could not be refreshed. Please try refreshing the details.";
    }

    private async Task<bool> RefreshVersionSummaryAsync()
    {
        var resource = _resource!;
        var generation = _generation;
        try
        {
            var json = await _module!.InvokeAsync<string>("getResource", _lifetime.Token, resource.Id, _requestKey);
            if (_disposed || generation != _generation || !ReferenceEquals(resource, _resource)) return false;
            var response = JsonSerializer.Deserialize<BrowserResponse>(json, JsonOptions);
            if (response is { Status: 401 or 403 or 404, Cancelled: false })
            {
                _resource = null;
                _errorStatus = response.Status;
                _errorBody = response.Body;
                return false;
            }
            if (response is { Status: 200, Cancelled: false } && !string.IsNullOrWhiteSpace(response.Body) &&
                JsonSerializer.Deserialize<ResourceReadDTO>(response.Body, JsonOptions) is { } updated && updated.Id == resource.Id)
            {
                // Keep the same resource instance so pending comments and their drafts survive.
                resource.Downloads = updated.Downloads;
                resource.UpdatedAt = updated.UpdatedAt;
                resource.CanEdit = updated.CanEdit;
                resource.CanDelete = updated.CanDelete;
                resource.DeleteBlockReason = updated.DeleteBlockReason;
                return true;
            }
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or JsonException or OperationCanceledException)
        {
            if (!_disposed) Logger.LogDebug(exception, "Resource details could not be refreshed after a version deletion.");
        }
        if (!_disposed && generation == _generation && ReferenceEquals(resource, _resource))
        {
            resource.CanDelete = false;
            resource.DeleteBlockReason = null;
        }
        return false;
    }

    private async Task LoadSectionAsync<T>(ResourceSectionState<T> section, string route) where T : class
    {
        if (_module is null || _resource is null || _disposed || (section.Started && section.Loading)) return;
        var resourceId = ResourceId;
        var generation = _generation;
        var revision = ++section.Revision;
        section.Started = true;
        section.Loading = true;
        section.ErrorStatus = null;
        section.ErrorBody = null;
        bool IsCurrent() => !_disposed && generation == _generation && resourceId == ResourceId &&
            _resource?.Id == resourceId && section.Revision == revision;
        try
        {
            var response = await new ResourceApiClient(_module, _requestKey, _lifetime.Token).ReadSectionAsync<T>(resourceId, route);
            if (!IsCurrent()) return;
            if (!response.Success)
            {
                section.ErrorStatus = response.Status;
                section.ErrorBody = response.Body;
                return;
            }
            section.Data = response.Data;
        }
        catch (JSDisconnectedException)
        {
            if (IsCurrent()) section.ErrorStatus = 0;
        }
        catch (OperationCanceledException) when (_disposed) { }
        catch (Exception exception) when (exception is JSException or JsonException or OperationCanceledException)
        {
            if (!IsCurrent()) return;
            section.ErrorStatus = exception is JsonException ? 502 : 0;
            Logger.LogDebug(exception, "Resource {ResourceId} section {Section} could not be loaded.", resourceId, route);
        }
        finally
        {
            if (IsCurrent())
            {
                section.Loading = false;
                StateHasChanged();
            }
        }
    }
}
