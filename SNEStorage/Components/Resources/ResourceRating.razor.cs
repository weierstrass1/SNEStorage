using System.Globalization;
using Microsoft.AspNetCore.Components;
using SNEStorage.Models;

namespace SNEStorage.Components.Resources;

public partial class ResourceRating
{
    protected static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-US");
    private int _hoveredValue;
    private long? _previousResourceId;
    private string? _previousUserId;
    [Parameter] public long ResourceId { get; set; }
    [Parameter] public Score? Score { get; set; }
    [Parameter] public string? CurrentUserId { get; set; }
    [Parameter] public bool IsAuthenticated { get; set; }
    [Parameter] public bool Saving { get; set; }
    [Parameter] public bool IsOwnResource { get; set; }
    [Parameter] public bool IsPrivate { get; set; }
    [Parameter] public string? Message { get; set; }
    [Parameter] public EventCallback<int> OnRate { get; set; }
    protected IReadOnlyList<ScoreUsers> Votes => Score?.ScoresUsers.Where(vote => vote.Value is >= 1 and <= 5).ToArray() ?? [];
    protected double Average => Votes.Count == 0 ? 0 : Votes.Average(vote => vote.Value);
    protected string FillWidth => (Average / 5 * 100).ToString("0.###", CultureInfo.InvariantCulture) + "%";
    protected string RatingLabel => $"{Average.ToString("0.#", DisplayCulture)} out of 5 stars";
    protected bool CanSubmit => IsAuthenticated && !IsOwnResource && !IsPrivate && OnRate.HasDelegate;
    protected int SelectedValue { get; private set; }
    protected int HighlightedValue => _hoveredValue > 0 ? _hoveredValue : SelectedValue;
    protected override void OnParametersSet()
    {
        if (_previousResourceId == ResourceId && _previousUserId == CurrentUserId) return;
        _previousResourceId = ResourceId;
        _previousUserId = CurrentUserId;
        SelectedValue = (int)(Votes.FirstOrDefault(vote => vote.UserId == CurrentUserId)?.Value ?? 0);
        _hoveredValue = 0;
    }
    protected void Hover(int value) => _hoveredValue = value;
    protected void ClearHover() => _hoveredValue = 0;
    protected void Select(int value) => SelectedValue = value;
    protected Task SaveAsync() => CanSubmit && !Saving && SelectedValue is >= 1 and <= 5
        ? OnRate.InvokeAsync(SelectedValue) : Task.CompletedTask;
}
