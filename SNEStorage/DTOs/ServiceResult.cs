using System.Runtime.CompilerServices;

namespace SNEStorage.DTOs;

public enum ServiceResultTypes
{
    // General
    Success,
    Valid,
    Invalid,
    Error,
    Unauthorized,
    DBError,
    CacheError,
    CancellationTokenError,
    CancelledToken,
    Unavailable,
    Disabled,

    // Validación y paginación
    InvalidSort,
    UndefinedSort,
    InvalidScoreRange,
    InvalidDateRange,
    InvalidName,
    InvalidDescription,
    InvalidRepositoryURL,
    InvalidReleaseNote,
    InvalidVersion,
    InvalidMaxAmount,
    SkipOutOfRange,
    MaxAmountSurpassTheLimit,
    NegativePage,
    InvalidPage,
    InvalidScore,
    InvalidText,
    InvalidTitle,
    UndefinedLike,

    AmountOfElementsSurpassTheLimit,
    AmountOfCommentsSurpassTheLimit,
    InvalidAmountOfCommentsPerPage,
    PreviewAmountSurpassTheLimit,

    EmptyComment,
    EmptyName,
    EmptyDescription,
    EmptyTypeList,

    // Recursos, versiones y comentarios
    ResourceNotFound,
    ResourceTypeNotFound,
    VersionNotFound,
    VersionAlreadyExists,
    VersionHasSealOfQuality,
    CantDeleteOnlyVersion,
    CommentNotFound,
    LikeNotFound,

    CantAddScoreToPrivateResource,
    CantScoreSelfResource,
    CantLikeDeletedComment,
    CantReplyDeletedComment,
    CantLikeYourOwnComment,

    // Usuarios y cuentas
    UserNotFound,
    UserAlreadyExists,
    PasswordChangeError,
    InvalidEmail,
    InvalidPassword,
    InvalidToken,
    AvatarLoadError,
    UserCreationError,
    TokenCreationError,
    AccountLockedOut,

    // Archivos
    FileNotFound,
    InvalidFileForContext,
    FileCreationError,
    FileDeletionError,
    InvalidFile,
    EmptyFile,
    FormReadingError,

    BatchCreationError,
    BatchError,
    UpdateBatchError,

    InvalidMime,
    MimeMismatch,

    InvalidImage,
    InvalidImageSize,
    InvalidImageNumberOfFrames,
    ImageSurpassFramesLimit,
    InvalidVideo,

    MediaSizeSurpassTheLimit,
    CompressedFileSizeSurpassTheLimit,
    FileSizeSurpassTheLimit,
    PreviewSizeSurpassTheLimit,
    AvatarSizeSurpassTheLimit,

    // Email
    TemplateLoadError,
    EmailConnectionError,

    // Noticias
    NewNotFound,

    // Flags
    FlagNotFound,
    RepeatedFlag,

    //Reasons
    ReasonNotFound,
    InvalidSearch,
    ResourceHasSealOfQuality,
    ResourceHasDependents,

    // Discord
    InvalidUserId,
    InvalidDiscordUserId,
    InvalidDiscordUsername,
    DiscordAccountNotLinked,
    EmailNotConfirmed,
    BannedUser,
    DiscordAccountLinkedToAnotherUser,
    InvalidOAuthCode,
    OAuthTokenExchangeError,
    OAuthUserLookupError,

    // Notificaciones hacia el bot
    InvalidEvent,
    InvalidConfiguration,
    EntityNotFound,
    Ineligible,
    UnsupportedEvent,

    InvalidTag,
    InvalidExternalAuthor,
}
public enum ActionResultType
{
    Ok,
    NoContent,
    PhysicalFile,
    Redirect,

    BadRequest,
    Unauthorized,
    Forbidden,
    NotFound,
    Conflict,

    PayloadTooLarge,
    UnsupportedMediaType,
    ClientClosedRequest,

    InternalServerError,
    BadGateway,
    ServiceUnavailable,
    NotImplemented
}
public partial class ServiceResult
{
    public ServiceResultTypes Type { get; }
    public ActionResultType ActionType { get; }
    public LogLevel Severity { get; }
    private ServiceResult(ServiceResultTypes type, ActionResultType actionType, LogLevel severity)
    {
        Type = type;
        ActionType = actionType;
        Severity = severity;
    }
    public ServiceResultContext Return<TService>(ILogger<TService> logger, Exception? exception = null,
        [CallerFilePath] string filePath = "", [CallerMemberName] string methodName = "",
        [CallerLineNumber] int line = 0)
    {
        ServiceResultContext result = new(this, filePath, methodName, line, exception);
        if (Severity != LogLevel.None && logger.IsEnabled(Severity))
            logger.Log(Severity, exception,
                "{ErrorType} in {Service}.{Method} at line {Line} : {message}",
                Type, filePath, methodName, line, result.Message);
        return result;
    }
}
public class ServiceResultContext
{
    public ServiceResult Result { get; }
    public string FilePath { get; }
    public string MethodName { get; }
    public int Line { get; }
    public string? Message { get; }
    internal ServiceResultContext(ServiceResult result, string filePath, string methodName, int line, Exception? exception = null)
    {
        Result = result;
        FilePath = filePath;
        MethodName = methodName;
        Line = line;
        if (exception != null)
            Message = exception.Message;
    }
}
public sealed record PhysicalFileData(string Path, string ContentType, string DownloadName,
    bool EnableRangeProcessing = true);
