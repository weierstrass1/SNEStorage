using System.Text.Json;
using SNEStorage.Services;

namespace SNEStorage.Components;

// Presentation only: translates HTTP statuses and service enum values into user-facing copy.
internal static class ApiErrorPresentation
{
    public static ErrorCopy Describe(int status, string? body = null, string? domain = null)
    {
        var code = ReadCode(body, out var responseDomain);
        var captchaError = code switch
        {
            "AntiforgeryTokenMissing" => new ErrorCopy("Page verification unavailable", "Reload this page before submitting again.", code),
            "CaptchaRequired" => new ErrorCopy("Verification required", "Complete the security check and try again. Your changes have been kept.", code),
            "CaptchaInvalid" => new ErrorCopy("Verification expired or was rejected", "Please complete a new security check and try again. Your changes have been kept.", code),
            "CaptchaUnavailable" => new ErrorCopy("Verification is unavailable", "The security check could not be completed. Please try again later. Your changes have been kept.", code),
            "CaptchaCancelled" => new ErrorCopy("Verification cancelled", "Verification was cancelled. Your changes have been kept.", code),
            "CaptchaBusy" => new ErrorCopy("Verification in progress", "Finish the security check already open, then try this action again.", code),
            _ => null
        };
        if (captchaError is not null) return captchaError;
        if (domain == "account-upload")
        {
            var avatarError = AvatarError(code, status);
            if (avatarError is not null) return avatarError;
        }
        var source = (responseDomain ?? domain)?.Trim().ToLowerInvariant();
        // Upload actions can return several enums with overlapping numeric values.
        // Without a domain, a numeric error cannot be safely assigned to an enum.
        if (source is "resource-upload" or "account-upload")
        {
            if (int.TryParse(code, out _)) return StatusError(status);
            var specificUploadError = FileError(code);
            if (specificUploadError is not null) return specificUploadError;
            source = source == "resource-upload" ? "resource" : "account";
        }
        ErrorCopy? specific = source switch
        {
            "resource" => ResourceError(code, status),
            "comment" => CommentError(code),
            "account" or "register" or "login" or "profile" or "password-reset" => AccountError(code, domain == "password-reset"),
            "file" => FileError(code),
            "search" => SearchError(code),
            "email" => EmailError(code),
            "news" => NewsError(code, status),
            _ => null
        };
        if (specific is not null) return specific;
        if (source == "profile" && status == 404)
            return new("Account profile unavailable", "We could not load your account profile. Try logging in again. If the problem continues, contact SNEStorage staff.", SignIn: true);
        return StatusError(status);
    }

    private static string? ReadCode(string? body, out string? domain)
    {
        domain = null;
        if (string.IsNullOrWhiteSpace(body)) return null;
        try
        {
            using var document = JsonDocument.Parse(body);
            var value = document.RootElement;
            if (value.ValueKind == JsonValueKind.Object)
            {
                if (value.TryGetProperty("domain", out var source) && source.ValueKind == JsonValueKind.String)
                    domain = source.GetString();
                if (value.TryGetProperty("error", out var error)) value = error;
            }
            return value.ValueKind is JsonValueKind.Number or JsonValueKind.String
                ? ReadControllerCode(value.ToString(), ref domain)
                : null;
        }
        catch (JsonException)
        {
            // Error pages may receive the enum name directly in a query parameter.
            return ReadControllerCode(body.Trim(), ref domain);
        }
    }

    private static string ReadControllerCode(string code, ref string? domain)
    {
        var separator = code.IndexOf('-');
        if (separator < 0) return code;
        // GetType() includes the namespace; GetType().Name contains only the controller name.
        // Both forms identify the controller that produced the response.
        var controller = code[..separator];
        controller = controller[(controller.LastIndexOf('.') + 1)..].ToLowerInvariant();
        var source = controller switch
        {
            "resourcecontroller" => "resource",
            "commentcontroller" => "comment",
            "accountcontroller" or "registercontroller" or "logincontroller" or "profilecontroller" => "account",
            "filecontroller" => "file",
            "emailcontroller" => "email",
            "newscontroller" => "news",
            "getlikecontroller" or "usercontroller" or "videogamecontroller" or "externalauthorcontroller" => "search",
            _ => null
        };
        if (source is null) return code;
        domain = source;
        return code[(separator + 1)..];
    }

    public static bool IsSuccess(string? body, string domain)
    {
        var code = ReadCode(body, out var responseDomain);
        if (responseDomain is not null && !string.Equals(responseDomain, domain, StringComparison.OrdinalIgnoreCase)) return false;
        return domain switch
        {
            "resource" => Enum.TryParse<ResourceServiceResult>(code, true, out var resourceResult) && resourceResult == ResourceServiceResult.Success,
            "comment" => Enum.TryParse<CommentServiceResult>(code, true, out var commentResult) && commentResult == CommentServiceResult.Success,
            "account" => Enum.TryParse<AccountServiceResult>(code, true, out var accountResult) && accountResult == AccountServiceResult.Success,
            "email" => Enum.TryParse<EmailServiceResult>(code, true, out var emailResult) && emailResult == EmailServiceResult.Success,
            _ => false
        };
    }

    private static ErrorCopy? NewsError(string? code, int status) => code switch
    {
        "InvalidTitle" => new("Check the title", "Enter a title with 1–200 characters. Your text has been kept.", code),
        "InvalidText" => new("Add an announcement", "Write your announcement before publishing. Your text has been kept.", code),
        "NewNotFound" => new("News not found", "This announcement no longer exists. Your text has been kept.", code),
        "Unauthorized" when status == 401 => new("Log in to continue", "Your session could not be verified. Log in again; your text has been kept.", code, true),
        "Unauthorized" => new("Staff access required", "Only moderators and admins can publish news. Your text has been kept.", code),
        "UserNotFound" => new("Account unavailable", "Log in again to verify your account. Your text has been kept.", code, true),
        "DBError" => new("News could not be saved", "Please try again later. Your text has been kept.", code),
        _ => null
    };

    private static ErrorCopy? ResourceError(string? code, int status)
    {
        if (!Enum.TryParse<ResourceServiceResult>(code, true, out var error) || !Enum.IsDefined(error)) return null;
        return error switch
        {
            ResourceServiceResult.CacheError => new("Resource options are unavailable", "Resource options could not be loaded. Please try again later. Your submission was not completed.", error.ToString()),
            ResourceServiceResult.InvalidMaxAmount => new("Choose a valid page size", "Select a positive number of resources per request.", error.ToString()),
            ResourceServiceResult.MaxAmountSurpassTheLimit => new("The page size is too large", "Resources must be requested in blocks of at most 50.", error.ToString()),
            ResourceServiceResult.NegativePage => new("Choose a valid page", "The requested page number must not be negative.", error.ToString()),
            ResourceServiceResult.SkipOutOfRange => new("Resource page unavailable", "The requested page is too far beyond the supported range. Return to the first page and try again.", error.ToString()),
            ResourceServiceResult.EmptyComment => new("Add a comment", "Write a comment before posting.", error.ToString()),
            ResourceServiceResult.CantScoreSelfResource => new("You cannot rate your own resource", "Authors and submitters cannot rate this resource.", error.ToString()),
            ResourceServiceResult.InvalidScore => new("Choose a valid rating", "Select a rating between 1 and 5 stars.", error.ToString()),
            ResourceServiceResult.CantAddScoreToPrivateResource => new("Ratings are unavailable", "Private resources cannot be rated.", error.ToString()),
            ResourceServiceResult.PreviewAmountSurpassTheLimit => new("Too many previews", "A resource can have at most 20 previews. Remove some previews and try again.", error.ToString()),
            ResourceServiceResult.PreviewSizeSurpassTheLimit => new("A preview is too large", "Each preview must be no larger than 5 MB. Choose a smaller file and try again.", error.ToString()),
            ResourceServiceResult.FileSizeSurpassTheLimit => new("The archive is too large", "The resource archive must be no larger than 250 MB.", error.ToString()),
            ResourceServiceResult.EmptyName => new("Check the resource name", "Enter a resource name with at least 2 characters.", error.ToString()),
            ResourceServiceResult.InvalidName => new("Check the resource name", "Use 2–100 characters: letters, numbers and spaces between words.", error.ToString()),
            ResourceServiceResult.InvalidDescription => new("Check the description", "Enter a description with 10–10,000 characters.", error.ToString()),
            ResourceServiceResult.InvalidRepositoryURL => new("Check the repository link", "Enter a valid HTTP or HTTPS URL, up to 2,048 characters, without embedded credentials.", error.ToString()),
            ResourceServiceResult.InvalidReleaseNote => new("Check the release notes", "Enter release notes with 10–10,000 characters.", error.ToString()),
            ResourceServiceResult.AmountOfElementsSurpassTheLimit => new("Too many items", "Keep up to 50 authors in total, 50 dependencies in total and 50 tags per resource.", error.ToString()),
            ResourceServiceResult.InvalidSort => new("Choose a valid sorting option", "Select a supported resource sorting option.", error.ToString()),
            ResourceServiceResult.InvalidScoreRange => new("Check the rating range", "The minimum rating must not exceed the maximum rating.", error.ToString()),
            ResourceServiceResult.InvalidDateRange => new("Check the date range", "The start date must not be later than the end date. Choose a supported end date.", error.ToString()),
            ResourceServiceResult.EmptyDescription => new("Check the description", "Enter a description with at least 10 characters.", error.ToString()),
            ResourceServiceResult.EmptyTypeList => new("Choose a resource type", "Select at least one resource type before submitting.", error.ToString()),
            ResourceServiceResult.ResourceNotFound => new("Resource not found", "This resource does not exist or is no longer available.", error.ToString()),
            ResourceServiceResult.ResourceTypeNotFound => new("Resource type unavailable", "A selected resource type is no longer available. Reload the options and choose another type.", error.ToString()),
            ResourceServiceResult.VersionNotFound => new("Version not found", "The requested resource version does not exist or is no longer available.", error.ToString()),
            ResourceServiceResult.InvalidVersion => new("Check the version", "Enter a version such as 1, 1.2, 1.2.3, 1-alpha.10 or 2.50-beta, with 1–4 digits per number and at most 25 characters in total. A new version must exceed the previous versions; an edited version must keep its position in their order.", error.ToString()),
            ResourceServiceResult.VersionAlreadyExists => new("Version already exists", "This resource already has a version with that label. Choose a different version label.", error.ToString()),
            ResourceServiceResult.FileNotFound => new("File not found", "The requested file does not exist or is no longer available.", error.ToString()),
            ResourceServiceResult.UserNotFound when status == 401 => new("Your session could not be verified", "Log in to access this resource or submit your files.", error.ToString(), true),
            ResourceServiceResult.UserNotFound => new("Account unavailable", "A selected author account could not be found. Check the authors and try again.", error.ToString()),
            ResourceServiceResult.Unauthorized => new("Access denied", "Your account does not have permission to access this resource or perform this action.", error.ToString()),
            ResourceServiceResult.FileCreationError => new("The upload could not be saved", "We could not save your files. Please try again later.", error.ToString()),
            ResourceServiceResult.FileDeletionError => new("File cleanup could not be completed", "Reload the resource to check its current state before trying again.", error.ToString()),
            ResourceServiceResult.BatchCreationError => new("Uploads are temporarily unavailable", "We could not prepare storage for your files. Please try again later.", error.ToString()),
            ResourceServiceResult.DBError => new("Resource data is unavailable", "We could not complete the resource request. Please try again later.", error.ToString()),
            _ => null
        };
    }

    private static ErrorCopy? FileError(string? code)
    {
        if (!Enum.TryParse<FileServiceResult>(code, true, out var error) || !Enum.IsDefined(error)) return null;
        return error switch
        {
            FileServiceResult.FileCreationError => new("The upload could not be saved", "We could not save your files. Please try again later.", error.ToString()),
            FileServiceResult.CancellationTokenError => new("The upload was interrupted", "Your submission has not been confirmed. Check its current state before trying again.", error.ToString()),
            FileServiceResult.ImageSurpassFramesLimit => new("Too many image frames", "Animated images can contain at most 1,800 frames. Choose a shorter animation.", error.ToString()),
            FileServiceResult.InvalidImageSize => new("Unsupported image dimensions", "Choose an image with valid dimensions and no more than 4,194,304 pixels in total.", error.ToString()),
            FileServiceResult.InvalidImageNumberOfFrames or FileServiceResult.InvalidImage => new("The image could not be read", "Choose a complete, valid PNG, JPG, GIF, WebP or AVIF image.", error.ToString()),
            FileServiceResult.FormReadingError => new("The file could not be read", "Select the file again and retry the upload.", error.ToString()),
            FileServiceResult.InvalidMime => new("Unsupported file format", "Choose a file format supported by this upload.", error.ToString()),
            FileServiceResult.MimeMismatch => new("The file type does not match its contents", "Choose a file whose reported media type matches its actual format.", error.ToString()),
            FileServiceResult.CacheError => new("File options are unavailable", "File formats could not be verified. Please try again later.", error.ToString()),
            FileServiceResult.BatchError or FileServiceResult.UpdateBatchError => new("Uploads are temporarily unavailable", "We could not prepare storage for your files. Please try again later.", error.ToString()),
            FileServiceResult.DBError => new("File data is unavailable", "We could not save the file information. Please try again later.", error.ToString()),
            FileServiceResult.InvalidVideo => new("The video could not be read", "Choose a valid MP4, WebM or Ogg video.", error.ToString()),
            FileServiceResult.InvalidFile => new("The archive could not be accepted", "Choose a complete, nonempty ZIP, RAR, 7Z, TAR or GZ archive without encryption or split volumes, with at most 10,000 entries.", error.ToString()),
            FileServiceResult.EmptyFile => new("The file is empty", "Choose a file that contains data before uploading.", error.ToString()),
            FileServiceResult.MediaSizeSurpassTheLimit => new("The media file is too large", "Each image or video must be no larger than 5 MB.", error.ToString()),
            FileServiceResult.AvatarSizeSurpassTheLimit => new("The avatar is too large", "Choose an avatar no larger than 5 MB.", error.ToString()),
            FileServiceResult.CompressedFileSizeSurpassTheLimit or FileServiceResult.FileSizeSurpassTheLimit => new("The file is too large", "Each resource archive must be no larger than 250 MB.", error.ToString()),
            _ => null
        };
    }

    private static ErrorCopy? SearchError(string? code)
    {
        if (!Enum.TryParse<GetLikeResult>(code, true, out var error) || !Enum.IsDefined(error)) return null;
        return error switch
        {
            GetLikeResult.InvalidMaxAmount => new("Invalid search size", "The number of search results must be positive.", error.ToString()),
            GetLikeResult.MaxAmountSurpassTheLimit => new("Too many search results requested", "Searches can return at most 20 suggestions at a time.", error.ToString()),
            GetLikeResult.DBError => new("Search is unavailable", "Suggestions could not be loaded. Please try again later.", error.ToString()),
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

    private static ErrorCopy? AvatarError(string? code, int status)
    {
        // Numeric upload errors are ambiguous: several enums share the same values.
        if (int.TryParse(code, out _)) return status == 413 ?
            new("The avatar image is too large", "Your avatar image is too large. Choose an image no larger than 5 MB.") : null;
        return code switch
        {
            nameof(FileServiceResult.MediaSizeSurpassTheLimit) or
            nameof(FileServiceResult.FileSizeSurpassTheLimit) or
            nameof(FileServiceResult.AvatarSizeSurpassTheLimit) =>
                new("The avatar image is too large", "Your avatar image is too large. Choose an image no larger than 5 MB.", code),
            nameof(FileServiceResult.InvalidMime) or nameof(FileServiceResult.InvalidVideo) or
            nameof(FileServiceResult.InvalidFile) =>
                new("Unsupported avatar format", "Choose a PNG or JPG image for your avatar.", code),
            nameof(FileServiceResult.InvalidImage) or nameof(FileServiceResult.InvalidImageNumberOfFrames) =>
                new("The avatar image could not be read", "Choose a complete, valid PNG or JPG image.", code),
            nameof(FileServiceResult.EmptyFile) =>
                new("The avatar image is empty", "Choose an image that contains data.", code),
            _ when status == 413 =>
                new("The avatar image is too large", "Your avatar image is too large. Choose an image no larger than 5 MB."),
            _ => null
        };
    }

    public static string[] ValidationErrors(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return [];
        try
        {
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("errors", out var errors) ||
                errors.ValueKind != JsonValueKind.Object) return [];
            return errors.EnumerateObject().Where(field => field.Value.ValueKind == JsonValueKind.Array)
                .SelectMany(field => field.Value.EnumerateArray())
                .Where(value => value.ValueKind == JsonValueKind.String)
                .Select(value => value.GetString()!).Distinct().ToArray();
        }
        catch (JsonException) { return []; }
    }

    private static ErrorCopy? AccountError(string? code, bool passwordReset = false)
    {
        if (!Enum.TryParse<AccountServiceResult>(code, true, out var error) || !Enum.IsDefined(error)) return null;
        return error switch
        {
            AccountServiceResult.UserNotFound when passwordReset => new("This link is unavailable", "The account for this password reset link is no longer available. Request a new link or contact SNEStorage support.", error.ToString()),
            AccountServiceResult.InvalidToken when passwordReset => new("This link is invalid or has expired", "Request a new password reset email and open its complete link. A link cannot be reused after the password has been changed.", error.ToString()),
            AccountServiceResult.InvalidEmail => new("Check your email", "Enter a valid account email address.", error.ToString()),
            AccountServiceResult.InvalidPassword => new("Check your new password", "Enter a new password and make sure both password fields match.", error.ToString()),
            AccountServiceResult.PasswordChangeError => new("The password could not be changed", "Your password may not meet the account password requirements, or the change could not be saved. Try a stronger password. If the problem continues, contact SNEStorage support.", error.ToString()),
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
            EmailServiceResult.UserNotFound => new("Email delivery is unavailable", "We could not retrieve the account information needed to send this email.", error.ToString()),
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
        409 => new("The content has changed", "This action conflicts with the current state of the content. Reload it before trying again."),
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
