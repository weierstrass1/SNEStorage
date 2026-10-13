namespace SNEStorage.DTOs;

public partial class ServiceResult
{
    private static ServiceResult Create(
        ServiceResultTypes type,
        ActionResultType actionType,
        LogLevel severity = LogLevel.Information)
    {
        return new(type, actionType, severity);
    }

    // Resultados satisfactorios
    public static ServiceResult Success { get; } =
        Create(ServiceResultTypes.Success, ActionResultType.Ok, LogLevel.None);

    public static ServiceResult NoContent { get; } =
        Create(ServiceResultTypes.Success, ActionResultType.NoContent, LogLevel.None);

    public static ServiceResult PhysicalFile { get; } =
        Create(ServiceResultTypes.Success, ActionResultType.PhysicalFile, LogLevel.None);

    public static ServiceResult Redirect { get; } =
        Create(ServiceResultTypes.Success, ActionResultType.Redirect, LogLevel.None);

    public static ServiceResult Valid { get; } =
        Create(ServiceResultTypes.Valid, ActionResultType.Ok, LogLevel.None);

    public static ServiceResult Disabled { get; } =
        Create(ServiceResultTypes.Disabled, ActionResultType.Ok, LogLevel.Debug);

    // Errores generales
    public static ServiceResult Invalid { get; } =
        Create(ServiceResultTypes.Invalid, ActionResultType.BadRequest);

    public static ServiceResult Error { get; } =
        Create(
            ServiceResultTypes.Error,
            ActionResultType.InternalServerError,
            LogLevel.Error);

    public static ServiceResult Unauthorized { get; } =
        Create(
            ServiceResultTypes.Unauthorized,
            ActionResultType.Unauthorized,
            LogLevel.Warning);

    public static ServiceResult Forbidden { get; } =
        Create(
            ServiceResultTypes.Unauthorized,
            ActionResultType.Forbidden,
            LogLevel.Warning);

    public static ServiceResult DBError { get; } =
        Create(
            ServiceResultTypes.DBError,
            ActionResultType.InternalServerError,
            LogLevel.Error);

    public static ServiceResult CacheError { get; } =
        Create(
            ServiceResultTypes.CacheError,
            ActionResultType.InternalServerError,
            LogLevel.Error);

    public static ServiceResult CancellationTokenError { get; } =
        Create(
            ServiceResultTypes.CancellationTokenError,
            ActionResultType.InternalServerError,
            LogLevel.Error);

    public static ServiceResult RequestCancelled { get; } =
        Create(
            ServiceResultTypes.CancellationTokenError,
            ActionResultType.ClientClosedRequest,
            LogLevel.Debug);

    public static ServiceResult CancelledToken { get; } =
        Create(
            ServiceResultTypes.CancelledToken,
            ActionResultType.ClientClosedRequest,
            LogLevel.Debug);

    public static ServiceResult Unavailable { get; } =
        Create(
            ServiceResultTypes.Unavailable,
            ActionResultType.ServiceUnavailable,
            LogLevel.Error);

    // Validación y paginación
    public static ServiceResult InvalidSort { get; } =
        Create(ServiceResultTypes.InvalidSort, ActionResultType.BadRequest);

    public static ServiceResult UndefinedSort { get; } =
        Create(ServiceResultTypes.UndefinedSort, ActionResultType.BadRequest);

    public static ServiceResult InvalidScoreRange { get; } =
        Create(ServiceResultTypes.InvalidScoreRange, ActionResultType.BadRequest);

    public static ServiceResult InvalidDateRange { get; } =
        Create(ServiceResultTypes.InvalidDateRange, ActionResultType.BadRequest);

    public static ServiceResult InvalidName { get; } =
        Create(ServiceResultTypes.InvalidName, ActionResultType.BadRequest);

    public static ServiceResult InvalidDescription { get; } =
        Create(ServiceResultTypes.InvalidDescription, ActionResultType.BadRequest);

    public static ServiceResult InvalidRepositoryURL { get; } =
        Create(ServiceResultTypes.InvalidRepositoryURL, ActionResultType.BadRequest);

    public static ServiceResult InvalidReleaseNote { get; } =
        Create(ServiceResultTypes.InvalidReleaseNote, ActionResultType.BadRequest);

    public static ServiceResult InvalidVersion { get; } =
        Create(ServiceResultTypes.InvalidVersion, ActionResultType.BadRequest);

    public static ServiceResult InvalidMaxAmount { get; } =
        Create(ServiceResultTypes.InvalidMaxAmount, ActionResultType.BadRequest);

    public static ServiceResult SkipOutOfRange { get; } =
        Create(ServiceResultTypes.SkipOutOfRange, ActionResultType.BadRequest);

    public static ServiceResult MaxAmountSurpassTheLimit { get; } =
        Create(
            ServiceResultTypes.MaxAmountSurpassTheLimit,
            ActionResultType.BadRequest);

    public static ServiceResult NegativePage { get; } =
        Create(ServiceResultTypes.NegativePage, ActionResultType.BadRequest);

    public static ServiceResult InvalidPage { get; } =
        Create(ServiceResultTypes.InvalidPage, ActionResultType.BadRequest);

    public static ServiceResult InvalidScore { get; } =
        Create(ServiceResultTypes.InvalidScore, ActionResultType.BadRequest);

    public static ServiceResult InvalidText { get; } =
        Create(ServiceResultTypes.InvalidText, ActionResultType.BadRequest);

    public static ServiceResult InvalidTitle { get; } =
        Create(ServiceResultTypes.InvalidTitle, ActionResultType.BadRequest);

    public static ServiceResult UndefinedLike { get; } =
        Create(ServiceResultTypes.UndefinedLike, ActionResultType.BadRequest);

    public static ServiceResult AmountOfElementsSurpassTheLimit { get; } =
        Create(
            ServiceResultTypes.AmountOfElementsSurpassTheLimit,
            ActionResultType.BadRequest);

    public static ServiceResult AmountOfCommentsSurpassTheLimit { get; } =
        Create(
            ServiceResultTypes.AmountOfCommentsSurpassTheLimit,
            ActionResultType.BadRequest);

    public static ServiceResult InvalidAmountOfCommentsPerPage { get; } =
        Create(
            ServiceResultTypes.InvalidAmountOfCommentsPerPage,
            ActionResultType.BadRequest);

    public static ServiceResult PreviewAmountSurpassTheLimit { get; } =
        Create(
            ServiceResultTypes.PreviewAmountSurpassTheLimit,
            ActionResultType.BadRequest);

    public static ServiceResult EmptyComment { get; } =
        Create(ServiceResultTypes.EmptyComment, ActionResultType.BadRequest);

    public static ServiceResult EmptyName { get; } =
        Create(ServiceResultTypes.EmptyName, ActionResultType.BadRequest);

    public static ServiceResult EmptyDescription { get; } =
        Create(ServiceResultTypes.EmptyDescription, ActionResultType.BadRequest);

    public static ServiceResult EmptyTypeList { get; } =
        Create(ServiceResultTypes.EmptyTypeList, ActionResultType.BadRequest);

    // Recursos, versiones y comentarios
    public static ServiceResult ResourceNotFound { get; } =
        Create(ServiceResultTypes.ResourceNotFound, ActionResultType.NotFound);

    public static ServiceResult ResourceTypeNotFound { get; } =
        Create(ServiceResultTypes.ResourceTypeNotFound, ActionResultType.NotFound);

    public static ServiceResult VersionNotFound { get; } =
        Create(ServiceResultTypes.VersionNotFound, ActionResultType.NotFound);

    public static ServiceResult VersionAlreadyExists { get; } =
        Create(ServiceResultTypes.VersionAlreadyExists, ActionResultType.Conflict);

    public static ServiceResult CommentNotFound { get; } =
        Create(ServiceResultTypes.CommentNotFound, ActionResultType.NotFound);

    public static ServiceResult LikeNotFound { get; } =
        Create(ServiceResultTypes.LikeNotFound, ActionResultType.NotFound);

    public static ServiceResult CantAddScoreToPrivateResource { get; } =
        Create(
            ServiceResultTypes.CantAddScoreToPrivateResource,
            ActionResultType.Forbidden,
            LogLevel.Warning);

    public static ServiceResult CantScoreSelfResource { get; } =
        Create(
            ServiceResultTypes.CantScoreSelfResource,
            ActionResultType.Forbidden,
            LogLevel.Warning);

    public static ServiceResult CantLikeDeletedComment { get; } =
        Create(
            ServiceResultTypes.CantLikeDeletedComment,
            ActionResultType.Conflict);

    public static ServiceResult CantReplyDeletedComment { get; } =
        Create(
            ServiceResultTypes.CantReplyDeletedComment,
            ActionResultType.Conflict);

    public static ServiceResult CantLikeYourOwnComment { get; } =
        Create(
            ServiceResultTypes.CantLikeYourOwnComment,
            ActionResultType.Forbidden,
            LogLevel.Warning);

    // Usuarios y cuentas
    public static ServiceResult UserNotFound { get; } =
        Create(ServiceResultTypes.UserNotFound, ActionResultType.NotFound);

    // Variantes necesarias para conservar los comportamientos actuales
    public static ServiceResult UserNotFoundBadRequest { get; } =
        Create(ServiceResultTypes.UserNotFound, ActionResultType.BadRequest);

    public static ServiceResult UserNotFoundUnauthorized { get; } =
        Create(
            ServiceResultTypes.UserNotFound,
            ActionResultType.Unauthorized,
            LogLevel.Warning);

    public static ServiceResult UserAlreadyExists { get; } =
        Create(ServiceResultTypes.UserAlreadyExists, ActionResultType.Conflict);

    public static ServiceResult PasswordChangeError { get; } =
        Create(ServiceResultTypes.PasswordChangeError, ActionResultType.BadRequest);

    public static ServiceResult InvalidEmail { get; } =
        Create(ServiceResultTypes.InvalidEmail, ActionResultType.BadRequest);

    public static ServiceResult InvalidPassword { get; } =
        Create(ServiceResultTypes.InvalidPassword, ActionResultType.BadRequest);

    public static ServiceResult InvalidToken { get; } =
        Create(ServiceResultTypes.InvalidToken, ActionResultType.BadRequest);

    public static ServiceResult AvatarLoadError { get; } =
        Create(
            ServiceResultTypes.AvatarLoadError,
            ActionResultType.InternalServerError,
            LogLevel.Error);

    public static ServiceResult UserCreationError { get; } =
        Create(ServiceResultTypes.UserCreationError, ActionResultType.BadRequest);

    public static ServiceResult TokenCreationError { get; } =
        Create(
            ServiceResultTypes.TokenCreationError,
            ActionResultType.InternalServerError,
            LogLevel.Error);

    // Archivos
    public static ServiceResult FileNotFound { get; } =
        Create(ServiceResultTypes.FileNotFound, ActionResultType.NotFound);

    public static ServiceResult InvalidFileForContext { get; } =
        Create(
            ServiceResultTypes.InvalidFileForContext,
            ActionResultType.BadRequest);

    public static ServiceResult FileCreationError { get; } =
        Create(
            ServiceResultTypes.FileCreationError,
            ActionResultType.InternalServerError,
            LogLevel.Error);

    public static ServiceResult FileDeletionError { get; } =
        Create(
            ServiceResultTypes.FileDeletionError,
            ActionResultType.InternalServerError,
            LogLevel.Error);

    public static ServiceResult InvalidFile { get; } =
        Create(ServiceResultTypes.InvalidFile, ActionResultType.BadRequest);

    public static ServiceResult EmptyFile { get; } =
        Create(ServiceResultTypes.EmptyFile, ActionResultType.BadRequest);

    public static ServiceResult FormReadingError { get; } =
        Create(ServiceResultTypes.FormReadingError, ActionResultType.BadRequest);

    public static ServiceResult BatchCreationError { get; } =
        Create(
            ServiceResultTypes.BatchCreationError,
            ActionResultType.InternalServerError,
            LogLevel.Error);

    public static ServiceResult BatchError { get; } =
        Create(
            ServiceResultTypes.BatchError,
            ActionResultType.InternalServerError,
            LogLevel.Error);

    public static ServiceResult UpdateBatchError { get; } =
        Create(
            ServiceResultTypes.UpdateBatchError,
            ActionResultType.InternalServerError,
            LogLevel.Error);

    public static ServiceResult InvalidMime { get; } =
        Create(
            ServiceResultTypes.InvalidMime,
            ActionResultType.UnsupportedMediaType);

    public static ServiceResult MimeMismatch { get; } =
        Create(
            ServiceResultTypes.MimeMismatch,
            ActionResultType.UnsupportedMediaType);

    public static ServiceResult InvalidImage { get; } =
        Create(ServiceResultTypes.InvalidImage, ActionResultType.BadRequest);

    public static ServiceResult InvalidImageSize { get; } =
        Create(ServiceResultTypes.InvalidImageSize, ActionResultType.BadRequest);

    public static ServiceResult InvalidImageNumberOfFrames { get; } =
        Create(
            ServiceResultTypes.InvalidImageNumberOfFrames,
            ActionResultType.BadRequest);

    public static ServiceResult ImageSurpassFramesLimit { get; } =
        Create(
            ServiceResultTypes.ImageSurpassFramesLimit,
            ActionResultType.BadRequest);

    public static ServiceResult InvalidVideo { get; } =
        Create(ServiceResultTypes.InvalidVideo, ActionResultType.BadRequest);

    public static ServiceResult MediaSizeSurpassTheLimit { get; } =
        Create(
            ServiceResultTypes.MediaSizeSurpassTheLimit,
            ActionResultType.PayloadTooLarge);

    public static ServiceResult CompressedFileSizeSurpassTheLimit { get; } =
        Create(
            ServiceResultTypes.CompressedFileSizeSurpassTheLimit,
            ActionResultType.PayloadTooLarge);

    public static ServiceResult FileSizeSurpassTheLimit { get; } =
        Create(
            ServiceResultTypes.FileSizeSurpassTheLimit,
            ActionResultType.PayloadTooLarge);

    public static ServiceResult PreviewSizeSurpassTheLimit { get; } =
        Create(
            ServiceResultTypes.PreviewSizeSurpassTheLimit,
            ActionResultType.PayloadTooLarge);

    public static ServiceResult AvatarSizeSurpassTheLimit { get; } =
        Create(
            ServiceResultTypes.AvatarSizeSurpassTheLimit,
            ActionResultType.PayloadTooLarge);

    // Email
    public static ServiceResult TemplateLoadError { get; } =
        Create(
            ServiceResultTypes.TemplateLoadError,
            ActionResultType.InternalServerError,
            LogLevel.Error);

    public static ServiceResult EmailConnectionError { get; } =
        Create(
            ServiceResultTypes.EmailConnectionError,
            ActionResultType.ServiceUnavailable,
            LogLevel.Error);

    // Noticias
    public static ServiceResult NewNotFound { get; } =
        Create(ServiceResultTypes.NewNotFound, ActionResultType.NotFound);

    // Discord
    public static ServiceResult InvalidUserId { get; } =
        Create(ServiceResultTypes.InvalidUserId, ActionResultType.BadRequest);

    public static ServiceResult InvalidDiscordUserId { get; } =
        Create(
            ServiceResultTypes.InvalidDiscordUserId,
            ActionResultType.BadRequest);

    public static ServiceResult InvalidDiscordUsername { get; } =
        Create(
            ServiceResultTypes.InvalidDiscordUsername,
            ActionResultType.BadRequest);

    public static ServiceResult DiscordAccountNotLinked { get; } =
        Create(
            ServiceResultTypes.DiscordAccountNotLinked,
            ActionResultType.NotFound);

    public static ServiceResult EmailNotConfirmed { get; } =
        Create(
            ServiceResultTypes.EmailNotConfirmed,
            ActionResultType.Forbidden,
            LogLevel.Warning);

    public static ServiceResult BannedUser { get; } =
        Create(
            ServiceResultTypes.BannedUser,
            ActionResultType.Forbidden,
            LogLevel.Warning);

    public static ServiceResult DiscordAccountLinkedToAnotherUser { get; } =
        Create(
            ServiceResultTypes.DiscordAccountLinkedToAnotherUser,
            ActionResultType.Conflict);

    public static ServiceResult InvalidOAuthCode { get; } =
        Create(
            ServiceResultTypes.InvalidOAuthCode,
            ActionResultType.BadRequest);

    public static ServiceResult OAuthTokenExchangeError { get; } =
        Create(
            ServiceResultTypes.OAuthTokenExchangeError,
            ActionResultType.BadGateway,
            LogLevel.Error);

    public static ServiceResult OAuthUserLookupError { get; } =
        Create(
            ServiceResultTypes.OAuthUserLookupError,
            ActionResultType.BadGateway,
            LogLevel.Error);

    // Notificaciones hacia el bot
    public static ServiceResult InvalidEvent { get; } =
        Create(ServiceResultTypes.InvalidEvent, ActionResultType.BadRequest);

    public static ServiceResult InvalidConfiguration { get; } =
        Create(
            ServiceResultTypes.InvalidConfiguration,
            ActionResultType.InternalServerError,
            LogLevel.Error);

    public static ServiceResult EntityNotFound { get; } =
        Create(ServiceResultTypes.EntityNotFound, ActionResultType.NotFound);

    public static ServiceResult Ineligible { get; } =
        Create(
            ServiceResultTypes.Ineligible,
            ActionResultType.Forbidden,
            LogLevel.Warning);

    public static ServiceResult UnsupportedEvent { get; } =
        Create(
            ServiceResultTypes.UnsupportedEvent,
            ActionResultType.NotImplemented);
}
