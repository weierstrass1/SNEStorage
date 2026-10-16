using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using SNEStorage.Components.Shared;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Pages;

[RequireAccount(StaffOnly = true)]
public partial class Moderation : IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly Regex HexColor = new("\\A#(?:[0-9a-fA-F]{3}|[0-9a-fA-F]{6})\\z", RegexOptions.CultureInvariant);
    private readonly string _requestKey = Guid.NewGuid().ToString("N");
    private readonly CancellationTokenSource _lifetime = new();
    private readonly HashSet<string> _failedAvatars = [];
    private IJSObjectReference? _module;
    private List<FlagReadDTO> _reports = [];
    private string _status = "false", _lowestPriority = "", _highestPriority = "";
    private FlagSort _sort = FlagSort.HighestPriority;
    private string _appliedStatus = "false", _appliedLowestPriority = "", _appliedHighestPriority = "";
    private FlagSort _appliedSort = FlagSort.HighestPriority;
    private DateTime? _createdAtFrom, _createdAtTo, _appliedCreatedAtFrom, _appliedCreatedAtTo;
    private long? _reasonId, _appliedReasonId;
    private List<ReasonOption> _reasons = [];
    private bool _reasonsLoading = true;
    private string? _reasonsError, _filterError;
    private int _page, _revision;
    private long? _savingId;
    private bool _loading = true, _disposed, _canGoNext, _uncertain;
    private ErrorCopy? _error;
    private string? _notice;
    private bool _deletingResource;
    private bool Busy => _loading || _savingId is not null || _deletingResource;

    [CascadingParameter(Name = "VerifiedPageAccount")] public AccountPageContext? VerifiedAccount { get; set; }

    [Inject] private IJSRuntime JavaScript { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed) return;
        await LoadAsync();
        if (!_disposed) await LoadReasonsAsync();
    }

    private async Task ApplyFiltersAsync()
    {
        if (Busy) return;
        _filterError = null;
        if (int.TryParse(_lowestPriority, out var lowest) && int.TryParse(_highestPriority, out var highest) && lowest > highest)
            _filterError = "Minimum priority cannot be greater than maximum priority.";
        else if (_createdAtFrom > _createdAtTo)
            _filterError = "The start date cannot be after the end date.";
        else if (_createdAtFrom >= DateTime.MaxValue.Date || _createdAtTo >= DateTime.MaxValue.Date)
            _filterError = "Choose dates before December 31, 9999.";
        if (_filterError is not null) return;
        _appliedStatus = _status;
        _appliedLowestPriority = _lowestPriority;
        _appliedHighestPriority = _highestPriority;
        _appliedSort = _sort;
        _appliedReasonId = _reasonId;
        _appliedCreatedAtFrom = _createdAtFrom;
        _appliedCreatedAtTo = _createdAtTo;
        _page = 0;
        _notice = null;
        await LoadAsync();
    }

    private Task RefreshAsync() => Busy ? Task.CompletedTask : LoadAsync();
    private void ResourceDeletionBusyChanged(bool busy) => _deletingResource = busy;
    private async Task ResourceDeletedAsync()
    {
        _deletingResource = false;
        _notice = "Resource deleted. Its reports have also been removed.";
        await LoadAsync();
    }
    private async Task PreviousAsync() { if (Busy || _page == 0) return; _page--; await LoadAsync(); }
    private async Task NextAsync() { if (Busy || !_canGoNext) return; _page++; await LoadAsync(); }

    private async Task LoadAsync(bool returnToPrevious = true)
    {
        var revision = ++_revision;
        _loading = true;
        _error = null;
        StateHasChanged();
        try
        {
            _module ??= await JavaScript.InvokeAsync<IJSObjectReference>("import", _lifetime.Token, "./js/moderation.js?v=2");
            if (_disposed) return;
            var query = $"Type=Resource&maxAmount=20&page={_page}&sort={_appliedSort}";
            if (_appliedStatus.Length > 0) query += $"&Resolved={_appliedStatus}";
            if (_appliedLowestPriority.Length > 0) query += $"&LowestPriority={_appliedLowestPriority}";
            if (_appliedHighestPriority.Length > 0) query += $"&HighestPriority={_appliedHighestPriority}";
            if (_appliedReasonId is { } reasonId) query += $"&ReasonID={reasonId}";
            if (_appliedCreatedAtFrom is { } from) query += $"&CreatedAtFrom={from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
            if (_appliedCreatedAtTo is { } to) query += $"&CreatedAtTo={to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}";
            var json = await _module.InvokeAsync<string>("getReports", _lifetime.Token, _requestKey, query);
            if (_disposed || revision != _revision) return;
            var response = JsonSerializer.Deserialize<BrowserResponse>(json, JsonOptions);
            if (response is not { Status: 200 })
            {
                _reports = [];
                _canGoNext = false;
                _error = ApiErrorPresentation.Describe(response?.Status ?? 0, response?.Body, "flags");
                return;
            }
            _reports = JsonSerializer.Deserialize<List<FlagReadDTO>>(response.Body, JsonOptions)
                ?? throw new JsonException("Report list missing.");
            _canGoNext = _reports.Count == 20;
            _uncertain = false;
            _failedAvatars.Clear();
            if (_reports.Count == 0 && _page > 0 && returnToPrevious)
            {
                _page--;
                await LoadAsync(false);
                _canGoNext = false;
                if (!_disposed) StateHasChanged();
            }
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException or JsonException)
        {
            if (!_disposed && revision == _revision)
            {
                _reports = [];
                _canGoNext = false;
                _error = new("Reports unavailable", "Resource reports could not be loaded. Please try again.");
            }
        }
        finally
        {
            if (!_disposed && revision == _revision) { _loading = false; StateHasChanged(); }
        }
    }

    private async Task SetResolvedAsync(FlagReadDTO report)
    {
        if (Busy || _uncertain || _module is null || _disposed) return;
        _savingId = report.Id;
        _error = null;
        _notice = null;
        try
        {
            var json = await _module.InvokeAsync<string>("setResolved", _lifetime.Token, _requestKey, report.Id, !report.Resolved);
            if (_disposed) return;
            var response = JsonSerializer.Deserialize<BrowserResponse>(json, JsonOptions);
            if (response is { Status: >= 200 and < 300 } &&
                long.TryParse(response.Body, out var id) && id == report.Id)
            {
                _notice = $"Report #{report.Id} {(report.Resolved ? "reopened" : "resolved")}.";
                await LoadAsync();
            }
            else if (response is null || response.Status is 0 or 408 or 499 or 504 or (>= 200 and < 300))
                UnconfirmedUpdate();
            else _error = ApiErrorPresentation.Describe(response.Status, response.Body, "flags");
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException or JsonException)
        {
            if (!_disposed) UnconfirmedUpdate();
        }
        finally { _savingId = null; }
    }

    private void UnconfirmedUpdate()
    {
        _uncertain = true;
        _error = new("Update not confirmed", "Refresh the reports to check their status before making another change.");
    }

    private async Task LoadReasonsAsync()
    {
        if (_disposed) return;
        _reasonsLoading = true;
        _reasonsError = null;
        StateHasChanged();
        try
        {
            _module ??= await JavaScript.InvokeAsync<IJSObjectReference>("import", _lifetime.Token, "./js/moderation.js?v=2");
            if (_disposed) return;
            var json = await _module.InvokeAsync<string>("getReasons", _lifetime.Token, _requestKey);
            if (_disposed) return;
            var response = JsonSerializer.Deserialize<BrowserResponse>(json, JsonOptions);
            if (response is not { Status: 200 })
            {
                _reasonsError = ApiErrorPresentation.Describe(response?.Status ?? 0, response?.Body, "flags").Message;
                return;
            }
            _reasons = (JsonSerializer.Deserialize<Dictionary<long, ReasonOption>>(response.Body, JsonOptions)
                ?? throw new JsonException("Reason catalog missing.")).Values.OrderBy(r => r.Id).ToList();
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException or JsonException)
        {
            if (!_disposed) _reasonsError = "Reasons could not be loaded. Please try again.";
        }
        finally { if (!_disposed) { _reasonsLoading = false; StateHasChanged(); } }
    }

    private string SelectedReason => _reasonId is { } id ?
        _reasons.FirstOrDefault(r => r.Id == id)?.Description ?? $"Reason #{id}" : "All reasons";
    private void SelectReason(long? id) => _reasonId = id;

    private async Task ResetFiltersAsync()
    {
        if (Busy) return;
        _status = "false";
        _lowestPriority = _highestPriority = "";
        _sort = FlagSort.HighestPriority;
        _reasonId = null;
        _createdAtFrom = _createdAtTo = null;
        await ApplyFiltersAsync();
    }

    private static string PriorityName(int value) => value switch { 5 => "Critical", 4 => "High", 3 => "Medium", 2 => "Low", _ => "Minor" };
    private static string FormatDate(DateTime value) => value.ToString("MMM d, yyyy · HH:mm", CultureInfo.InvariantCulture);
    private static string UsernameColor(string? color) => color is not null && HexColor.IsMatch(color) ? color : "#dec4fa";
    private string AvatarUrl(UserNameDTO user) => _failedAvatars.Contains(user.Id) ?
        "/images/caja.png" : PublicFileUrl.Resolve(user.Avatar?.URL, "/images/caja.png");
    private void AvatarFailed(string userId) => _failedAvatars.Add(userId);

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        _revision++;
        _lifetime.Cancel();
        if (_module is not null)
        {
            try
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(1));
                await _module.InvokeVoidAsync("dispose", cleanup.Token, _requestKey);
                await _module.DisposeAsync();
            }
            catch (Exception exception) when (exception is JSDisconnectedException or JSException or OperationCanceledException) { }
        }
        _lifetime.Dispose();
    }

    private sealed class BrowserResponse
    {
        public int Status { get; set; }
        public string Body { get; set; } = "";
    }

    private sealed class ReasonOption
    {
        public long Id { get; set; }
        public string Description { get; set; } = "";
        public int Priority { get; set; }
        public long ReasonCategoryId { get; set; }
        public ReasonCategoryOption? ReasonCategory { get; set; }
    }

    private sealed class ReasonCategoryOption
    {
        public string Name { get; set; } = "";
    }
}
