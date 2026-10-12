using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using SNEStorage.Components.Resources;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Pages;

public partial class Resources : IAsyncDisposable
{
    private readonly string _requestKey = Guid.NewGuid().ToString("N");
    private IJSObjectReference? _module;
    private DotNetObjectReference<Resources>? _reference;
    private ResourceListApiClient? _api;
    private ElementReference _sentinel;
    private List<ResourceFinderViewDTO> _resources = [];
    private IReadOnlyList<ResourceTypeDTO> _types = [];
    private ResourceListFilterState _draft = new(), _applied = new();
    private ResourceSorting _sort = ResourceSorting.CreatedAt;
    private bool _descending = true;
    private int _pageSize = 20, _page, _blocks, _revision, _errorStatus;
    private int? _lastPage;
    private bool _loading = true, _loadingMore, _endReached, _filtersOpen, _optionsLoading, _optionsLoaded, _syncObserver, _disposed, _scrollPending;
    private string? _error, _errorBody, _filterError, _optionsError, _announcement;
    [Inject] private IJSRuntime JavaScript { get; set; } = default!;
    [Inject] private ILogger<Resources> Logger { get; set; } = default!;

    private int BlockSize => _pageSize is 20 or 50 ? _pageSize : 50;
    private int BlocksPerPage => _pageSize > 0 ? Math.Max(1, _pageSize / BlockSize) : 1;
    private bool ShowGame => _applied.Finder.VideogameIDs.Length != 1;
    private bool ShowTypes => _applied.Finder.TypeIds.Length != 1;
    private bool ShowCategories => _applied.Finder.CategoryIDs.Length != 1;
    private bool HasActiveFilters => _applied.Summary(_types).Any() || _applied.Finder.CanIncludesPorn ||
        _applied.Finder.CanIncludesGore || _applied.Finder.CanIncludesPolitics || _applied.Finder.CanIncludesSlurs || _applied.Finder.CanIncludesSensitiveContent;
    private bool CanLoadMore => !_loading && !_loadingMore && !_endReached && (_pageSize == 0 || _blocks < BlocksPerPage);
    private bool CanNextPage => !_loadingMore && _error is null && _blocks == BlocksPerPage && !_endReached && (_lastPage is null || _page < _lastPage);
    private string SortLabel => _sort.ToString();
    private string RangeLabel => $"Showing {(long)_page * _pageSize + 1:N0}–{(long)_page * _pageSize + _resources.Count:N0}";
    private string ContentSummary => string.Join(", ", new[] {
        _applied.Finder.CanIncludesPorn ? "sexual content allowed" : null,
        _applied.Finder.CanIncludesGore ? "gore allowed" : null,
        _applied.Finder.CanIncludesPolitics ? "politics allowed" : null,
        _applied.Finder.CanIncludesSlurs ? "slurs allowed" : null,
        _applied.Finder.CanIncludesSensitiveContent ? "other sensitive content allowed" : null }.OfType<string>()) is { Length: > 0 } text ? text : "hidden";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed) return;
        try
        {
            if (firstRender)
            {
                _module = await JavaScript.InvokeAsync<IJSObjectReference>("import", "./js/resource-list.js?v=3");
                if (_disposed) { await _module.DisposeAsync(); return; }
                _reference = DotNetObjectReference.Create(this);
                _api = new(_module, _requestKey);
                await ReloadAsync(0);
                return;
            }
            if (_module is not null && _syncObserver)
            {
                _syncObserver = false;
                await _module.InvokeVoidAsync("observe", _requestKey, _sentinel, _reference, CanLoadMore && _error is null);
            }
            if (_module is not null && _scrollPending)
            {
                _scrollPending = false;
                await _module.InvokeVoidAsync("focusResults", "library-results");
            }
        }
        catch (JSDisconnectedException) { }
        catch (JSException exception)
        {
            Logger.LogWarning(exception, "The resource list could not be initialized.");
            _loading = false;
            SetError(0, null);
            if (!_disposed) StateHasChanged();
        }
    }

    private async Task ReloadAsync(int targetPage)
    {
        if (_api is null || _module is null || _disposed) return;
        var oldItems = _resources;
        var oldPage = _page;
        var oldBlocks = _blocks;
        var oldEnd = _endReached;
        var revision = ++_revision;
        _page = targetPage;
        _blocks = 0;
        _resources = [];
        _loading = true;
        _loadingMore = false;
        _endReached = false;
        _error = _announcement = null;
        StateHasChanged();
        try { await _module.InvokeVoidAsync("cancelList", _requestKey); }
        catch (JSDisconnectedException) { return; }
        if (_disposed || revision != _revision) return;
        await ReadBlockAsync(revision);
        if (_disposed || revision != _revision) return;
        if (targetPage > oldPage && _error is null && _resources.Count == 0 && oldItems.Count > 0)
        {
            _resources = oldItems;
            _page = oldPage;
            _blocks = oldBlocks;
            _endReached = oldEnd;
            _lastPage = oldPage;
            _announcement = "You have reached the last page.";
        }
        _loading = false;
        _syncObserver = true;
        StateHasChanged();
    }

    private async Task ReadBlockAsync(int revision)
    {
        if (_api is null) return;
        var backendPage = (long)_page * BlocksPerPage + _blocks;
        if (backendPage * BlockSize > int.MaxValue)
        {
            _endReached = true;
            _announcement = "The end of the supported page range has been reached. Refine your filters to continue.";
            return;
        }
        try
        {
            var result = await _api.FindAsync(_applied.Copy(), BlockSize, (int)backendPage);
            if (_disposed || revision != _revision) return;
            if (!result.Success) { SetError(result.Status, result.Body); return; }
            var items = result.Data!;
            var known = _resources.Select(item => item.ResourceIdName.Id).ToHashSet();
            _resources.AddRange(items.Where(item => known.Add(item.ResourceIdName.Id)));
            _blocks++;
            _endReached = items.Count < BlockSize;
            if (_endReached) _lastPage = _page;
        }
        catch (JSDisconnectedException) { }
        catch (Exception exception) when (exception is JSException or JsonException)
        {
            if (!_disposed && revision == _revision)
            {
                Logger.LogWarning(exception, "The resource list could not be read.");
                SetError(502, null);
            }
        }
    }

    [JSInvokable]
    public async Task LoadMoreAsync()
    {
        if (!CanLoadMore || _disposed) return;
        var revision = _revision;
        _loadingMore = true;
        _error = null;
        StateHasChanged();
        await ReadBlockAsync(revision);
        if (_disposed || revision != _revision) return;
        _loadingMore = false;
        _syncObserver = true;
        StateHasChanged();
    }

    private Task RetryAsync() => _api is null ? OnAfterRenderAsync(true) : ReloadAsync(_page);
    private Task PreviousPageAsync() { _scrollPending = true; return ReloadAsync(_page - 1); }
    private Task NextPageAsync() { _scrollPending = true; return ReloadAsync(_page + 1); }
    private async Task OrderChangedAsync()
    {
        _applied.Finder.Sorting = new() { SortType = _sort, SortOrderDescending = _descending };
        _lastPage = null;
        await ReloadAsync(0);
    }
    private Task PageSizeChangedAsync() { _lastPage = null; return ReloadAsync(0); }
    private async Task ApplyFiltersAsync()
    {
        _filterError = _draft.Validate();
        if (_filterError is not null) return;
        _applied = _draft.Copy();
        _applied.Finder.Sorting = new() { SortType = _sort, SortOrderDescending = _descending };
        _lastPage = null;
        await ReloadAsync(0);
    }
    private async Task ResetFiltersAsync()
    {
        _draft = new();
        await ApplyFiltersAsync();
    }
    private async Task ToggleFiltersAsync()
    {
        _filtersOpen = !_filtersOpen;
        if (_filtersOpen && !_optionsLoaded) await LoadOptionsAsync();
    }
    private async Task LoadOptionsAsync()
    {
        if (_api is null || _optionsLoading || _disposed) return;
        _optionsLoading = true;
        _optionsError = null;
        try
        {
            var result = await _api.OptionsAsync();
            if (_disposed) return;
            if (result.Success) { _types = result.Data!.ResourceTypes; _optionsLoaded = true; }
            else _optionsError = "Filter choices could not be loaded.";
        }
        catch (JSDisconnectedException) { }
        catch (Exception exception) when (exception is JSException or JsonException)
        {
            Logger.LogWarning(exception, "Resource filter options could not be loaded.");
            _optionsError = "Filter choices could not be loaded.";
        }
        finally { _optionsLoading = false; }
    }
    private void SetError(int status, string? body)
    {
        _errorStatus = status;
        _errorBody = body;
        _error = ApiErrorPresentation.Describe(status, body, "resource").Message;
    }
    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        ++_revision;
        try
        {
            if (_module is not null)
            {
                await _module.InvokeVoidAsync("dispose", _requestKey);
                await _module.DisposeAsync();
            }
        }
        catch (JSDisconnectedException) { }
        catch (OperationCanceledException) { }
        finally { _reference?.Dispose(); }
    }
}
