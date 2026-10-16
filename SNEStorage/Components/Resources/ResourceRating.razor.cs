using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using SNEStorage.DTOs;

namespace SNEStorage.Components.Resources;

public partial class ResourceRating
{
    protected static readonly CultureInfo DisplayCulture = CultureInfo.GetCultureInfo("en-US");
    private long? _previousResourceId;
    private string? _previousUserId;
    private long? _previousRating;
    [Parameter] public long ResourceId { get; set; }
    [Parameter] public RatingReadDto? Rating { get; set; }
    [Parameter] public string? CurrentUserId { get; set; }
    [Parameter] public bool IsAuthenticated { get; set; }
    [Parameter] public bool Saving { get; set; }
    [Parameter] public bool IsOwnResource { get; set; }
    [Parameter] public bool IsPrivate { get; set; }
    [Parameter] public bool PermissionsReady { get; set; } = true;
    [Parameter] public string? Message { get; set; }
    [Parameter] public EventCallback<int> OnRate { get; set; }
    protected int VoteCount => Rating?.VoteCount ?? 0;
    protected double Average => VoteCount == 0 ? 0 : Math.Clamp(Rating?.Average ?? 0, 0, 5);
    protected string FillWidth => (Average / 5 * 100).ToString("0.###", CultureInfo.InvariantCulture) + "%";
    protected string RatingLabel => $"{Average.ToString("0.#", DisplayCulture)} out of 5 stars";
    protected bool CanSubmit => PermissionsReady && IsAuthenticated && !IsOwnResource && !IsPrivate && OnRate.HasDelegate;
    protected int SelectedValue { get; private set; }
    protected string VoteState => JsonSerializer.Serialize(new { mode = "rating", context = $"rating:{ResourceId}:{CurrentUserId}", average = Average, count = VoteCount, myRating = Rating?.MyRating, selected = SelectedValue });
    protected override void OnParametersSet()
    {
        if (_previousResourceId == ResourceId && _previousUserId == CurrentUserId && _previousRating == Rating?.MyRating) return;
        _previousResourceId = ResourceId;
        _previousUserId = CurrentUserId;
        _previousRating = Rating?.MyRating;
        SelectedValue = (int)(Rating?.MyRating is >= 1 and <= 5 ? Rating.MyRating.Value : 0);
    }
    protected void Select(int value) => SelectedValue = value;
    protected Task SaveAsync() => CanSubmit && !Saving && SelectedValue is >= 1 and <= 5
        ? OnRate.InvokeAsync(SelectedValue) : Task.CompletedTask;
}
