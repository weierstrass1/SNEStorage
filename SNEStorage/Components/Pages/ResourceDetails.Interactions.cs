using System.Text.Json;
using Microsoft.JSInterop;
using SNEStorage.Models;

namespace SNEStorage.Components.Pages;

public partial class ResourceDetails
{
    private bool _savingRating;
    private string? _ratingMessage;
    private bool _savingComment;
    private string? _commentMessage;
    private int _commentSubmissionRevision;
    private Comment? _publishedComment;

    protected async Task RateAsync(int value)
    {
        if (_savingRating || !_isAuthenticated || CanManage || value is < 1 or > 5 || _module is null || _resource is null || _resource.VisibilityId == 3) return;
        var resource = _resource;
        var userId = _currentUserId;
        var generation = _generation;
        var accountGeneration = _accountGeneration;
        _savingRating = true;
        _ratingMessage = null;
        try
        {
            var json = await _module.InvokeAsync<string>("rateResource", _lifetime.Token, resource.Id, value, _requestKey);
            if (!InteractionIsCurrent(resource, generation, accountGeneration)) return;
            var response = JsonSerializer.Deserialize<BrowserResponse>(json, JsonOptions);
            if (response is not { Status: >= 200 and < 300, Cancelled: false })
            {
                _ratingMessage = ApiErrorPresentation.Describe(response?.Status ?? 502, response?.Body, "resource").Message;
                return;
            }
            var vote = JsonSerializer.Deserialize<ScoreUsers>(response.Body ?? string.Empty, JsonOptions);
            if (vote is null || vote.Id <= 0 || vote.ScoreId != resource.ScoreId || vote.UserId != userId || vote.Value != value)
            {
                _ratingMessage = "We could not confirm your saved rating. Reload the resource to check it.";
                return;
            }
            // Display the saved result returned by ResourceController; do not refetch the whole resource.
            resource.Score ??= new Score { Id = resource.ScoreId };
            foreach (var previous in resource.Score.ScoresUsers.Where(item => item.UserId == vote.UserId).ToArray())
                resource.Score.ScoresUsers.Remove(previous);
            resource.Score.ScoresUsers.Add(vote);
            _ratingMessage = "Your rating has been saved.";
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or JsonException or OperationCanceledException)
        {
            if (InteractionIsCurrent(resource, generation, accountGeneration))
            {
                _ratingMessage = "We could not confirm your saved rating. Reload the resource before trying again.";
                Logger.LogDebug(exception, "Rating for resource {ResourceId} could not be confirmed.", resource.Id);
            }
        }
        finally
        {
            if (!_disposed && generation == _generation) _savingRating = false;
        }
    }

    protected async Task PostCommentAsync(string text)
    {
        if (_savingComment || !_isAuthenticated || string.IsNullOrWhiteSpace(text) || _module is null || _resource is null) return;
        var resource = _resource;
        var userId = _currentUserId;
        var generation = _generation;
        var accountGeneration = _accountGeneration;
        _savingComment = true;
        _commentMessage = null;
        try
        {
            var json = await _module.InvokeAsync<string>("postResourceComment", _lifetime.Token, resource.Id, text, _requestKey);
            if (!InteractionIsCurrent(resource, generation, accountGeneration)) return;
            var response = JsonSerializer.Deserialize<BrowserResponse>(json, JsonOptions);
            if (response is not { Status: >= 200 and < 300, Cancelled: false })
            {
                _commentMessage = response is { Status: > 0, Cancelled: false }
                    ? ApiErrorPresentation.Describe(response.Status, response.Body, "resource").Message
                    : "We could not confirm your comment was posted. Reload the resource before posting again; your text has been kept.";
                return;
            }
            var saved = JsonSerializer.Deserialize<ResourceComments>(response.Body ?? string.Empty, JsonOptions);
            if (saved is null || saved.Id <= 0 || saved.ResourceId != resource.Id ||
                saved.Comment is not { Id: > 0 } comment || comment.UserId != userId)
            {
                _commentMessage = "We could not confirm your comment was posted. Reload the resource before posting again; your text has been kept.";
                return;
            }
            _publishedComment = comment;
            _commentSubmissionRevision++;
            _commentMessage = "Your comment has been posted.";
        }
        catch (Exception exception) when (exception is JSDisconnectedException or JSException or JsonException or OperationCanceledException)
        {
            if (InteractionIsCurrent(resource, generation, accountGeneration))
            {
                _commentMessage = "We could not confirm your comment was posted. Reload the resource before posting again; your text has been kept.";
                Logger.LogDebug(exception, "Comment for resource {ResourceId} could not be confirmed.", resource.Id);
            }
        }
        finally
        {
            if (!_disposed && generation == _generation) _savingComment = false;
        }
    }

    private bool InteractionIsCurrent(Resource resource, int generation, int accountGeneration) =>
        !_disposed && ReferenceEquals(_resource, resource) && resource.Id == ResourceId &&
        generation == _generation && accountGeneration == _accountGeneration;
}
