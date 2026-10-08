using System.Text.Json;
using SNEStorage.Services;

namespace SNEStorage.Components;

// Presentation only: translates HTTP statuses and service enum values into user-facing copy.
internal static class ApiErrorPresentation
{
    public static ErrorCopy Describe(int status, string? body = null, string? domain = null)
    {
        var code = ReadCode(body);
        var source = domain?.Trim().ToLowerInvariant();
        ErrorCopy? specific = source switch
        {
            "resource" => ResourceError(code),
            "comment" => CommentError(code),
            "account" or "register" or "login" or "profile" => AccountError(code),
            "email" => EmailError(code),
            _ => null
        };
        return specific ?? StatusError(status);
    }

    private static string? ReadCode(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var document = JsonDocument.Parse(body);
            var value = document.RootElement;
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("error", out var error))
                value = error;
            return value.ValueKind is JsonValueKind.Number or JsonValueKind.String
                ? value.ToString()
                : null;
        }
        catch (JsonException)
        {
            // Error pages may receive the enum name directly in a query parameter.
            return body.Trim();
        }
    }

    private static ErrorCopy? ResourceError(string? code)
    {
        if (!Enum.TryParse<ResourceServiceResult>(code, true, out var error) || !Enum.IsDefined(error)) return null;
        return error switch
        {
            ResourceServiceResult.EmptyComment => new("Add a comment", "Write a comment before posting.", error.ToString()),
            ResourceServiceResult.CantScoreSelfResource => new("You cannot rate your own resource", "Authors and submitters cannot rate this resource.", error.ToString()),
            ResourceServiceResult.InvalidScore => new("Choose a valid rating", "Select a rating between 1 and 5 stars.", error.ToString()),
            ResourceServiceResult.CantAddScoreToPrivateResource => new("Ratings are unavailable", "Private resources cannot be rated.", error.ToString()),
            ResourceServiceResult.PreviewAmountSurpassTheLimit => new("Too many previews", "A resource can have at most 20 previews. Remove some previews and try again.", error.ToString()),
            ResourceServiceResult.PreviewSizeSurpassTheLimit => new("A preview is too large", "Each preview must be no larger than 5 MB. Choose a smaller file and try again.", error.ToString()),
            ResourceServiceResult.FileSizeSurpassTheLimit => new("The archive is too large", "The resource archive must be no larger than 250 MB.", error.ToString()),
            ResourceServiceResult.EmptyName => new("Check the resource name", "Enter a resource name with at least 2 characters.", error.ToString()),
            ResourceServiceResult.EmptyDescription => new("Check the description", "Enter a description with at least 10 characters.", error.ToString()),
            ResourceServiceResult.EmptyTypeList => new("Choose a resource type", "Select at least one resource type before submitting.", error.ToString()),
            ResourceServiceResult.ResourceNotFound => new("Resource not found", "This resource does not exist or is no longer available.", error.ToString()),
            ResourceServiceResult.ResourceTypeNotFound => new("Resource type unavailable", "A selected resource type is no longer available. Reload the options and choose another type.", error.ToString()),
            ResourceServiceResult.VersionNotFound => new("Version not found", "The requested resource version does not exist or is no longer available.", error.ToString()),
            ResourceServiceResult.VersionAlreadyExists => new("Version already exists", "This resource already has a version with that label. Choose a different version label.", error.ToString()),
            ResourceServiceResult.FileNotFound => new("File not found", "The requested file does not exist or is no longer available.", error.ToString()),
            ResourceServiceResult.UserNotFound => new("Your session could not be verified", "Log in to access this resource or submit your files.", error.ToString(), true),
            ResourceServiceResult.Unauthorized => new("Access denied", "Your account does not have permission to access this resource or perform this action.", error.ToString()),
            ResourceServiceResult.FileCreationError => new("The upload could not be saved", "Check your file format and try again. Your submission was not completed.", error.ToString()),
            ResourceServiceResult.BatchCreationError => new("Uploads are temporarily unavailable", "We could not prepare storage for your files. Please try again later.", error.ToString()),
            ResourceServiceResult.DBError => new("Resource data is unavailable", "We could not complete the resource request. Please try again later.", error.ToString()),
            _ => null
        };
    }

    private static ErrorCopy? CommentError(string? code)
    {
        if (!Enum.TryParse<CommentServiceResult>(code, true, out var error) || !Enum.IsDefined(error)) return null;
        return error switch
        {
            CommentServiceResult.EmptyComment => new("Add a comment", "Write a comment before posting.", error.ToString()),
            CommentServiceResult.LikeNotFound => new("Vote not found", "Your vote is no longer present. Refresh the comments to see the current votes.", error.ToString()),
            CommentServiceResult.UndefinedLike => new("Invalid vote", "Choose like or dislike and try again.", error.ToString()),
            CommentServiceResult.CantLikeYourOwnComment => new("Voting unavailable", "You cannot like or dislike your own comment.", error.ToString()),
            CommentServiceResult.CantLikeDeletedComment => new("Voting unavailable", "You cannot like or dislike a deleted comment.", error.ToString()),
            CommentServiceResult.CantReplyDeletedComment => new("Reply unavailable", "This comment has been deleted and no longer accepts replies.", error.ToString()),
            CommentServiceResult.UndefinedSort => new("Sorting unavailable", "Choose a supported comment sorting option.", error.ToString()),
            CommentServiceResult.AmountOfCommentsSurpassTheLimit => new("Too many comments requested", "The requested page exceeds the comment limit. Refresh the comments to try again.", error.ToString()),
            CommentServiceResult.InvalidAmountOfCommentsPerPage => new("Invalid page size", "The requested comment page size is invalid.", error.ToString()),
            CommentServiceResult.NegativePage or CommentServiceResult.SkipOutOfRange => new("Comment page unavailable", "The requested comment page is invalid. Return to the first page and try again.", error.ToString()),
            CommentServiceResult.ResourceNotFound => new("Resource not found", "The resource associated with these comments is no longer available.", error.ToString()),
            CommentServiceResult.CommentNotFound => new("Comment not found", "This comment is no longer available.", error.ToString()),
            CommentServiceResult.Unauthorized => new("Access denied", "Your account cannot perform this action on this comment.", error.ToString()),
            CommentServiceResult.DBError => new("Comment data is unavailable", "We could not complete the comment request. Please try again later.", error.ToString()),
            _ => null
        };
    }

    private static ErrorCopy? AccountError(string? code)
    {
        if (!Enum.TryParse<AccountServiceResult>(code, true, out var error) || !Enum.IsDefined(error)) return null;
        return error switch
        {
            AccountServiceResult.UserNotFound => new("Account not found", "We could not find the account for this request. Please log in again.", error.ToString(), true),
            AccountServiceResult.InvalidToken => new("This link is invalid or has expired", "Open the latest confirmation link from your email. If you have already confirmed your email, you can log in.", error.ToString(), true),
            AccountServiceResult.UserAlreadyExists => new("This account already exists", "Use a different username or email address, or log in to your existing account.", error.ToString(), true),
            AccountServiceResult.CancelledToken => new("The request was interrupted", "Please try again when you are ready.", error.ToString()),
            AccountServiceResult.AvatarLoadError => new("The avatar could not be saved", "Check your image format and try again.", error.ToString()),
            AccountServiceResult.UserCreationError => new("The account could not be created", "Check your registration details and try again.", error.ToString()),
            AccountServiceResult.TokenCreationError => new("Authentication is unavailable", "We could not prepare authentication for your account. Please try again later.", error.ToString()),
            AccountServiceResult.DBError or AccountServiceResult.Error => new("Account data is unavailable", "We could not complete the account request. Please try again later.", error.ToString()),
            _ => null
        };
    }

    private static ErrorCopy? EmailError(string? code)
    {
        if (!Enum.TryParse<EmailServiceResult>(code, true, out var error) || !Enum.IsDefined(error)) return null;
        return error switch
        {
            EmailServiceResult.TemplateLoadError or EmailServiceResult.EmailConnectionError => new("The email could not be sent", "Email delivery is temporarily unavailable. Please try again later.", error.ToString()),
            EmailServiceResult.UserNotFound => new("Email confirmation is unavailable", "We could not retrieve the account information needed for this email.", error.ToString()),
            _ => null
        };
    }

    private static ErrorCopy StatusError(int status) => status switch
    {
        0 => new("Connection unavailable", "We could not reach SNEStorage. Check your connection and try again."),
        400 or 422 => new("The request could not be accepted", "Check your information and try again."),
        401 => new("Please log in", "Your session is missing or has expired. Log in to continue.", SignIn: true),
        403 => new("Access denied", "You do not have permission to access this page or perform this action."),
        404 => new("Page not found", "The page or resource you requested could not be found."),
        405 => new("This action is unavailable", "This request is not supported at this address."),
        408 or 504 => new("The request timed out", "The server took too long to respond. Please try again."),
        410 => new("This content is no longer available", "The requested content has been removed or has expired."),
        413 => new("The upload is too large", "Your files exceed the server's upload limit. Choose smaller files and try again."),
        415 => new("Unsupported file format", "Choose a file format supported by this upload."),
        429 => new("Please wait a moment", "Too many requests were received. Try again in a little while."),
        499 => new("The request was interrupted", "Please try again when you are ready."),
        502 or 503 => new("SNEStorage is temporarily unavailable", "Please come back in a moment."),
        _ => new("Something went wrong", "We could not complete your request. Please try again later.")
    };
}

internal sealed record ErrorCopy(string Title, string Message, string? Code = null, bool SignIn = false);
