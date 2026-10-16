using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SNEStorage.Services;

public class AccountService(
    UserManager<ApplicationUser> userManager,
    IWebHostEnvironment environment,
    IConfiguration configuration,
    SignInManager<ApplicationUser> signInManager,
    SnestorageContext context,
    FileService fileService,
    IHttpContextAccessor httpContextAccessor,
    EmailService emailService,
    DBHelperService dbHelperService,
    DiscordService discordService,
    ILogger<AccountService> logger)
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly IWebHostEnvironment _environment = environment;
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly IConfiguration _configuration = configuration;
    private readonly SnestorageContext _context = context;
    private readonly FileService _fileService = fileService;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly EmailService _emailService = emailService;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly DiscordService _discordService = discordService;
    private readonly ILogger<AccountService> _logger = logger;

    public async Task<ServiceResultContext> ChangePassword(
        ChangePasswordDTO dto, CancellationToken ct = default)
    {
        if (dto.UserId?.Length > TextLimits.USER_ID || dto.Token?.Length > TextLimits.TOKEN)
            return ServiceResult.InvalidToken.Return(_logger);
        if (dto.NewPassword?.Length > TextLimits.PASSWORD || dto.ConfirmPassword?.Length > TextLimits.PASSWORD)
            return ServiceResult.InvalidPassword.Return(_logger);

        if (string.IsNullOrWhiteSpace(dto.UserId) ||
            string.IsNullOrWhiteSpace(dto.Token) ||
            string.IsNullOrWhiteSpace(dto.NewPassword) ||
            dto.NewPassword != dto.ConfirmPassword)
            return ServiceResult.InvalidPassword.Return(_logger);

        var tokenResult = _dbHelperService.TryFunction(
            () => Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(dto.Token)),
            ServiceResult.InvalidToken);

        if (!tokenResult.TryPickT0(out var token, out var tokenError))
            return tokenError;

        if (string.IsNullOrEmpty(token))
            return ServiceResult.InvalidToken.Return(_logger);

        var userResult = await _dbHelperService.TryFunction(
            _ => _userManager.FindByIdAsync(dto.UserId),
            ServiceResult.DBError, ct: ct);

        if (!userResult.TryPickT0(out var user, out var userError))
            return userError;

        if (user is null)
            return ServiceResult.UserNotFound.Return(_logger);

        var resetResult = await _dbHelperService.TryFunction(
            _ => _userManager.ResetPasswordAsync(user, token, dto.NewPassword),
            ServiceResult.DBError, ct: ct);

        if (!resetResult.TryPickT0(out var result, out var resetError))
            return resetError;

        if (result.Succeeded)
            return ServiceResult.Success.Return(_logger);

        return result.Errors.Any(error =>
            error.Code == nameof(IdentityErrorDescriber.InvalidToken)) ?
                ServiceResult.InvalidToken.Return(_logger) :
                ServiceResult.PasswordChangeError.Return(_logger);
    }

    public async Task<ServiceResultContext> RequestPasswordReset(
        string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > TextLimits.EMAIL)
            return ServiceResult.InvalidEmail.Return(_logger);

        var userResult = await _dbHelperService.TryFunction(
            _ => _userManager.FindByEmailAsync(email.Trim()),
            ServiceResult.DBError, ct: ct);

        if (!userResult.TryPickT0(out var user, out var userError))
            return userError;

        if (user is null)
            return ServiceResult.Success.Return(_logger);

        var tokenResult = await _dbHelperService.TryFunction(
            _ => _userManager.GeneratePasswordResetTokenAsync(user),
            ServiceResult.TokenCreationError, ct: ct);

        if (!tokenResult.TryPickT0(out var token, out var tokenError))
            return tokenError;

        string encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        return await _emailService.PasswordReset(
            user.UserName!, user.Id, user.Email!, encodedToken, ct);
    }

    public async Task<ServiceResultContext> RequestEmailConfirmation(
        string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > TextLimits.EMAIL)
            return ServiceResult.InvalidEmail.Return(_logger);

        var userResult = await _dbHelperService.TryFunction(
            _ => _userManager.FindByEmailAsync(email.Trim()),
            ServiceResult.DBError, ct: ct);

        if (!userResult.TryPickT0(out var user, out var userError))
            return userError;

        if (user is null || user.EmailConfirmed)
            return ServiceResult.Success.Return(_logger);

        var tokenResult = await _dbHelperService.TryFunction(
            _ => _userManager.GenerateEmailConfirmationTokenAsync(user),
            ServiceResult.TokenCreationError, ct: ct);

        if (!tokenResult.TryPickT0(out var token, out var tokenError))
            return tokenError;

        string encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

        return await _emailService.Confirmation(
            user.UserName!, user.Id, user.Email!, encodedToken, ct);
    }

    public async Task<OneOf<RegisterResponseDTO, ServiceResultContext>> Register(
        RegisterInfo registerInfo, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(registerInfo.User) || registerInfo.User.Length > TextLimits.USERNAME)
            return ServiceResult.InvalidName.Return(_logger);
        if (string.IsNullOrWhiteSpace(registerInfo.Email) || registerInfo.Email.Length > TextLimits.EMAIL)
            return ServiceResult.InvalidEmail.Return(_logger);
        if (string.IsNullOrWhiteSpace(registerInfo.Password) ||
            registerInfo.Password.Length > TextLimits.PASSWORD ||
            registerInfo.PasswordRepeat?.Length > TextLimits.PASSWORD ||
            registerInfo.Password != registerInfo.PasswordRepeat)
            return ServiceResult.InvalidPassword.Return(_logger);

        var user = new ApplicationUser
        {
            UserName = registerInfo.User,
            Email = registerInfo.Email
        };

        string? avatarPath = GetAvatarUrl(registerInfo.Avatar.ContentType, user.Id);

        if (avatarPath is null)
            return ServiceResult.InvalidFileForContext.Return(_logger);

        var batchResult = await _fileService.BeginBatch(ct);

        if (!batchResult.TryPickT0(out var batchPath, out var batchError))
            return batchError;

        var fileResult = await _fileService.PrepareFile(
            FileContext.Avatar, registerInfo.Avatar, avatarPath, batchPath, ct: ct);

        if (!fileResult.TryPickT0(out var avatar, out var fileError))
            return fileError;

        var transactionResult = await _dbHelperService.TryFunction(
            cancel => _context.Database.BeginTransactionAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (!transactionResult.TryPickT0(out var transaction, out var transactionError))
        {
            _ = await _fileService.BatchRollback(batchPath, CancellationToken.None);
            return transactionError;
        }

        await using var transactionScope = transaction;

        var creationResult = await _dbHelperService.TryFunction(
            _ => _userManager.CreateAsync(user, registerInfo.Password),
            ServiceResult.DBError, ct: ct);

        if (!creationResult.TryPickT0(out var identityResult, out var creationError))
        {
            _ = await _dbHelperService.TryFunction(
                cancel => transaction.RollbackAsync(cancel),
                ServiceResult.DBError, ct: CancellationToken.None);

            _ = await _fileService.BatchRollback(batchPath, CancellationToken.None);
            return creationError;
        }

        if (!identityResult.Succeeded)
        {
            _ = await _dbHelperService.TryFunction(
                cancel => transaction.RollbackAsync(cancel),
                ServiceResult.DBError, ct: CancellationToken.None);

            _ = await _fileService.BatchRollback(batchPath, CancellationToken.None);
            return ServiceResult.UserCreationError.Return(_logger);
        }

        var info = new UserInfo
        {
            UserId = user.Id,
            UsernameColor = registerInfo.UsernameColor,
            Birthday = registerInfo.Birthday,
            TimeZoneId = registerInfo.TimeZoneId,
            EmailVisibilityId = registerInfo.EmailVisibilityId,
            Avatar = avatar
        };

        _context.Add(info);

        var saveResult = await _dbHelperService.TryFunction(
            cancel => _context.SaveChangesAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (!saveResult.TryPickT0(out _, out var saveError))
        {
            _ = await _dbHelperService.TryFunction(
                cancel => transaction.RollbackAsync(cancel),
                ServiceResult.DBError, ct: CancellationToken.None);

            _ = await _fileService.BatchRollback(batchPath, CancellationToken.None);
            return saveError;
        }

        var commitResult = await _dbHelperService.TryFunction(
            cancel => transaction.CommitAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (commitResult.Result != ServiceResult.Success)
        {
            _ = await _dbHelperService.TryFunction(
                cancel => transaction.RollbackAsync(cancel),
                ServiceResult.DBError, ct: CancellationToken.None);

            _ = await _fileService.BatchRollback(batchPath, CancellationToken.None);
            return commitResult;
        }

        _ = _fileService.BatchComplete(batchPath);

        bool confirmationEmailSent = false;

        var confirmationTokenResult = await _dbHelperService.TryFunction(
            _ => _userManager.GenerateEmailConfirmationTokenAsync(user),
            ServiceResult.TokenCreationError, ct: ct);

        if (confirmationTokenResult.TryPickT0(out var confirmationToken, out _) &&
            !string.IsNullOrWhiteSpace(confirmationToken))
        {
            string encodedToken = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(confirmationToken));

            var emailResult = await _emailService.Confirmation(
                user.UserName!, user.Id, user.Email!, encodedToken, ct);

            confirmationEmailSent = emailResult.Result == ServiceResult.Success;
        }

        return new RegisterResponseDTO
        {
            UserId = user.Id,
            ConfirmationEmailSent = confirmationEmailSent
        };
    }

    public async Task<OneOf<AuthenticationResponse, ServiceResultContext>> Login(
        LoginCredentials credentials)
    {
        if (string.IsNullOrWhiteSpace(credentials.User) ||
            credentials.User.Length > TextLimits.USERNAME ||
            string.IsNullOrWhiteSpace(credentials.Password) ||
            credentials.Password.Length > TextLimits.LOGIN_PASSWORD)
            return ServiceResult.InvalidPassword.Return(_logger);

        var signInResult = await _dbHelperService.TryFunction(
            _ => _signInManager.PasswordSignInAsync(
                credentials.User, credentials.Password,
                isPersistent: true, lockoutOnFailure: true),
            ServiceResult.DBError);

        if (!signInResult.TryPickT0(out var result, out var signInError))
            return signInError;

        if (result.IsLockedOut)
            return ServiceResult.AccountLockedOut.Return(_logger);

        if (result.IsNotAllowed)
            return ServiceResult.EmailNotConfirmed.Return(_logger);

        if (!result.Succeeded)
            return ServiceResult.InvalidPassword.Return(_logger);

        return await BuildToken(credentials.User);
    }

    public async Task<ServiceResultContext> Logout()
    {
        var context = _httpContextAccessor.HttpContext;

        if (context is null)
            return ServiceResult.Invalid.Return(_logger);

        var signOutResult = await _dbHelperService.TryFunction(
            _ => _signInManager.SignOutAsync(), ServiceResult.DBError);

        if (signOutResult.Result != ServiceResult.Success)
            return signOutResult;

        context.Session.Remove("Token");
        context.Response.Cookies.Delete("__Host-SNEStorage.Auth", new()
        {
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = "/"
        });

        return ServiceResult.Success.Return(_logger);
    }

    public async Task<OneOf<UserInfo, ServiceResultContext>> GetUserInfo(
        CancellationToken ct)
    {
        string? userId = GetUserID();

        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult.UserNotFound.Return(_logger);

        return await GetUserInfo(userId, ct);
    }

    public async Task<OneOf<UserInfo, ServiceResultContext>> GetUserInfo(
        string userId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId) || userId.Length > TextLimits.USER_ID)
            return ServiceResult.InvalidUserId.Return(_logger);

        var query = _context.UserInfos.AsNoTracking()
            .Include(info => info.TimeZone)
            .Include(info => info.EmailVisibility)
            .Include(info => info.Avatar)
            .Include(info => info.User)
            .Where(info => info.UserId == userId);

        var userResult = await _dbHelperService.TryFunction(
            cancel => query.FirstOrDefaultAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (!userResult.TryPickT0(out var user, out var userError))
            return userError;

        if (user is null)
            return ServiceResult.UserNotFound.Return(_logger);

        user.UserSummary = UserNameDTO.FromUserInfo(user, this);
        return user;
    }

    public async Task<OneOf<AuthenticationResponse, ServiceResultContext>> RefreshToken()
    {
        string? username = _httpContextAccessor.HttpContext?
            .User.FindFirstValue("user");

        if (string.IsNullOrWhiteSpace(username))
            return ServiceResult.InvalidToken.Return(_logger);

        return await BuildToken(username);
    }

    public async Task<OneOf<AuthenticationResponse, ServiceResultContext>> BuildToken(
        string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            return ServiceResult.InvalidToken.Return(_logger);

        var userResult = await _dbHelperService.TryFunction(
            _ => _userManager.FindByNameAsync(username),
            ServiceResult.DBError);

        if (!userResult.TryPickT0(out var user, out var userError))
            return userError;

        if (user is null)
            return ServiceResult.InvalidToken.Return(_logger);

        var confirmationResult = await _dbHelperService.TryFunction(
            _ => _userManager.IsEmailConfirmedAsync(user),
            ServiceResult.DBError);

        if (!confirmationResult.TryPickT0(
            out bool emailConfirmed, out var confirmationError))
            return confirmationError;

        if (!emailConfirmed || string.IsNullOrWhiteSpace(user.SecurityStamp))
            return ServiceResult.InvalidToken.Return(_logger);

        var claimsResult = await _dbHelperService.TryFunction(
            _ => _userManager.GetClaimsAsync(user),
            ServiceResult.DBError);

        if (!claimsResult.TryPickT0(out var userClaims, out var claimsError))
            return claimsError;

        var claims = new List<Claim>
        {
            new("user", username),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.UserName!),
            new("security_stamp", user.SecurityStamp)
        };

        claims.AddRange(userClaims);

        string? jwtKey = _configuration["JWTKey"];

        if (string.IsNullOrWhiteSpace(jwtKey))
            return ServiceResult.TokenCreationError.Return(_logger);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        DateTime expiration = DateTime.UtcNow.AddDays(90);

        var jwt = new JwtSecurityToken(
            claims: claims, expires: expiration, signingCredentials: credentials);

        var serializedTokenResult = _dbHelperService.TryFunction(
            () => new JwtSecurityTokenHandler().WriteToken(jwt),
            ServiceResult.TokenCreationError);

        if (!serializedTokenResult.TryPickT0(
            out var serializedToken, out var tokenError))
            return tokenError;

        return new AuthenticationResponse
        {
            Token = serializedToken,
            ExpireTime = expiration
        };
    }

    public async Task<ServiceResultContext> AddRole(EditRole editRole)
    {
        if (string.IsNullOrWhiteSpace(editRole.RoleName) || editRole.RoleName.Length > TextLimits.ROLE ||
            string.IsNullOrWhiteSpace(editRole.Username) || editRole.Username.Length > TextLimits.USERNAME)
            return ServiceResult.Invalid.Return(_logger);

        var userResult = await _dbHelperService.TryFunction(
            _ => _userManager.FindByNameAsync(editRole.Username),
            ServiceResult.DBError);

        if (!userResult.TryPickT0(out var user, out var userError))
            return userError;

        if (user is null)
            return ServiceResult.UserNotFoundBadRequest.Return(_logger);

        var claimsResult = await _dbHelperService.TryFunction(
            _ => _userManager.GetClaimsAsync(user),
            ServiceResult.DBError);

        if (!claimsResult.TryPickT0(out var claims, out var claimsError))
            return claimsError;

        if (claims.Any(claim =>
            claim.Type == editRole.RoleName && claim.Value == "1"))
            return ServiceResult.Invalid.Return(_logger);

        var addResult = await _dbHelperService.TryFunction(
            _ => _userManager.AddClaimAsync(
                user, new Claim(editRole.RoleName, "1")),
            ServiceResult.DBError);

        if (!addResult.TryPickT0(out var identityResult, out var addError))
            return addError;

        if (!identityResult.Succeeded)
            return ServiceResult.Invalid.Return(_logger);

        var stampResult = await _dbHelperService.TryFunction(
            _ => _userManager.UpdateSecurityStampAsync(user),
            ServiceResult.DBError);

        if (!stampResult.TryPickT0(out identityResult, out var stampError))
            return stampError;

        return identityResult.Succeeded ?
            ServiceResult.NoContent.Return(_logger) :
            ServiceResult.Invalid.Return(_logger);
    }

    public async Task<ServiceResultContext> RemoveRole(EditRole editRole)
    {
        if (string.IsNullOrWhiteSpace(editRole.RoleName) || editRole.RoleName.Length > TextLimits.ROLE ||
            string.IsNullOrWhiteSpace(editRole.Username) || editRole.Username.Length > TextLimits.USERNAME)
            return ServiceResult.Invalid.Return(_logger);

        var userResult = await _dbHelperService.TryFunction(
            _ => _userManager.FindByNameAsync(editRole.Username),
            ServiceResult.DBError);

        if (!userResult.TryPickT0(out var user, out var userError))
            return userError;

        if (user is null)
            return ServiceResult.UserNotFoundBadRequest.Return(_logger);

        var claimsResult = await _dbHelperService.TryFunction(
            _ => _userManager.GetClaimsAsync(user),
            ServiceResult.DBError);

        if (!claimsResult.TryPickT0(out var claims, out var claimsError))
            return claimsError;

        var claim = claims.FirstOrDefault(item =>
            item.Type == editRole.RoleName && item.Value == "1");

        if (claim is null)
            return ServiceResult.Invalid.Return(_logger);

        var removeResult = await _dbHelperService.TryFunction(
            _ => _userManager.RemoveClaimAsync(user, claim),
            ServiceResult.DBError);

        if (!removeResult.TryPickT0(out var identityResult, out var removeError))
            return removeError;

        if (!identityResult.Succeeded)
            return ServiceResult.Invalid.Return(_logger);

        var stampResult = await _dbHelperService.TryFunction(
            _ => _userManager.UpdateSecurityStampAsync(user),
            ServiceResult.DBError);

        if (!stampResult.TryPickT0(out identityResult, out var stampError))
            return stampError;

        return identityResult.Succeeded ?
            ServiceResult.NoContent.Return(_logger) :
            ServiceResult.Invalid.Return(_logger);
    }

    public async Task<ServiceResultContext> EditUserInfo(EditProfileDTO dto, 
        CancellationToken ct)
    {
        string? username = _httpContextAccessor.HttpContext?
            .User.FindFirstValue(ClaimTypes.Name);

        if (string.IsNullOrWhiteSpace(username))
            return ServiceResult.Invalid.Return(_logger);

        string? userId = GetUserID();

        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult.Invalid.Return(_logger);

        var infoQuery = _context.UserInfos
            .Where(info => info.UserId == userId);

        var infoResult = await _dbHelperService.TryFunction(
            cancel => infoQuery.FirstOrDefaultAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (!infoResult.TryPickT0(out var info, out var infoError))
            return infoError;

        if (info is null)
            return ServiceResult.UserNotFound.Return(_logger);

        var previousAvatarId = info.AvatarId;
        Models.File? newAvatar = null;
        string? batchPath = null;
        List<Models.File>? deletedAvatar = null;

        if (dto.File is not null && !string.IsNullOrWhiteSpace(dto.File.FileName))
        {
            string? avatarPath = GetAvatarUrl(dto.File.ContentType, userId);

            if (avatarPath is null)
                return ServiceResult.InvalidMime.Return(_logger);

            var batchResult = await _fileService.BeginBatch(ct);

            if (!batchResult.TryPickT0(out batchPath, out var batchError))
                return batchError;

            var fileResult = await _fileService.PrepareFile(
                FileContext.Avatar, dto.File, avatarPath, batchPath, ct: ct);

            if (!fileResult.TryPickT0(out newAvatar, out var fileError))
                return fileError;

            info.Avatar = newAvatar;
            _context.ChangeTracker.DetectChanges();

            var deletionResult = await _fileService.PrepareDeletion([previousAvatarId], ct);
            if(!deletionResult.TryPickT0(out deletedAvatar, out var deletionError))
            {
                await _fileService.BatchRollback(batchPath, CancellationToken.None);
                return deletionError;
            }
        }

        bool discordColorChanged = dto.UsernameColor is not null &&
            !string.Equals(dto.UsernameColor, info.UsernameColor,
                StringComparison.OrdinalIgnoreCase);

        info.UsernameColor = dto.UsernameColor ?? info.UsernameColor;
        info.Bio = dto.Bio ?? info.Bio;
        info.Patreon = GetSocialNetwork(dto.Patreon, info.Patreon);
        info.Paypal = GetSocialNetwork(dto.Paypal, info.Paypal);
        info.BuyMeACoffee = GetSocialNetwork(dto.BuyMeACoffee, info.BuyMeACoffee);
        info.Discord = GetSocialNetwork(dto.Discord, info.Discord);
        info.Twitter = GetSocialNetwork(dto.Twitter, info.Twitter);
        info.Facebook = GetSocialNetwork(dto.Facebook, info.Facebook);
        info.Youtube = GetSocialNetwork(dto.Youtube, info.Youtube);
        info.Instagram = GetSocialNetwork(dto.Instagram, info.Instagram);
        info.Steam = GetSocialNetwork(dto.Steam, info.Steam);
        info.Twitch = GetSocialNetwork(dto.Twitch, info.Twitch);
        info.Github = GetSocialNetwork(dto.Github, info.Github);
        info.NintendoNetwork = GetSocialNetwork(
            dto.NintendoNetwork, info.NintendoNetwork);
        info.TimeZoneId = dto.TimeZoneId ?? info.TimeZoneId;
        info.EmailVisibilityId = dto.EmailVisibilityId ?? info.EmailVisibilityId;

        ServiceResultContext saveResult = batchPath is null ?
            await _dbHelperService.TrySyncContext(ct: ct) :
            await _fileService.TrySyncContextWithFiles(batchPath, ct: ct);

        if (saveResult.Result != ServiceResult.Success)
            return saveResult;

        if (deletedAvatar != null)
            _fileService.FinishDeletion(deletedAvatar);

        if (discordColorChanged && !string.IsNullOrWhiteSpace(info.DiscordUserId))
            await _discordService.NotifyMemberChangedAsync(info.DiscordUserId, ct);

        return ServiceResult.Success.Return(_logger);
    }

    public string? GetUserID()
    {
        return _httpContextAccessor.HttpContext?
            .User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    public async Task<ServiceResultContext> ConfirmUser(
        string userId, string token64, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(userId) || userId.Length > TextLimits.USER_ID ||
            string.IsNullOrWhiteSpace(token64) || token64.Length > TextLimits.TOKEN)
            return ServiceResult.InvalidToken.Return(_logger);

        var userResult = await _dbHelperService.TryFunction(
            _ => _userManager.FindByIdAsync(userId),
            ServiceResult.DBError, ct: ct);

        if (!userResult.TryPickT0(out var user, out var userError))
            return userError;

        if (user is null)
            return ServiceResult.UserNotFound.Return(_logger);

        bool wasAlreadyConfirmed = user.EmailConfirmed;

        var tokenBytesResult = _dbHelperService.TryFunction(
            () => WebEncoders.Base64UrlDecode(token64),
            ServiceResult.InvalidToken);

        if (!tokenBytesResult.TryPickT0(out var tokenBytes, out var tokenError))
            return tokenError;

        string token = Encoding.UTF8.GetString(tokenBytes);

        var confirmationResult = await _dbHelperService.TryFunction(
            _ => _userManager.ConfirmEmailAsync(user, token),
            ServiceResult.DBError, ct: ct);

        if (!confirmationResult.TryPickT0(
            out var identityResult, out var confirmationError))
            return confirmationError;

        if (!identityResult.Succeeded)
            return ServiceResult.InvalidToken.Return(_logger);

        if (!wasAlreadyConfirmed)
        {
            var notification = new DiscordBotEventDTO(
                Guid.NewGuid(), DiscordBotEventTypes.UserRegistered,
                user.Id, DateTimeOffset.UtcNow);

            _ = await _discordService.NotifyBotAsync(notification, ct);
        }

        return ServiceResult.Success.Return(_logger);
    }

    public bool IsModeratorOrAdmin()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true &&
            (user.HasClaim("Admin", "1") || user.HasClaim("Moderator", "1"));
    }

    public async Task<OneOf<ApplicationUser, ServiceResultContext>> GetUser()
    {
        string? userId = GetUserID();

        if (string.IsNullOrWhiteSpace(userId))
            return ServiceResult.UserNotFound.Return(_logger);

        var userResult = await _dbHelperService.TryFunction(
            async cancel => await _context.Users.FindAsync([userId], cancel),
            ServiceResult.DBError);

        if (!userResult.TryPickT0(out var user, out var userError))
            return userError;

        return user is null ?
            ServiceResult.UserNotFound.Return(_logger) : user;
    }

    public async Task<OneOf<bool, ServiceResultContext>> ValidateJwt(
        string? userId, string? stamp, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(stamp))
            return false;

        var stampQuery = _context.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => user.SecurityStamp);

        var stampResult = await _dbHelperService.TryFunction(
            cancel => stampQuery.FirstOrDefaultAsync(cancel),
            ServiceResult.DBError, ct: ct);

        if (!stampResult.TryPickT0(out var currentStamp, out var stampError))
            return stampError;

        return currentStamp is not null &&
            string.Equals(currentStamp, stamp, StringComparison.Ordinal);
    }

    private static string? GetSocialNetwork(string? dtoValue, string? defaultValue)
    {
        if (dtoValue is null)
            return defaultValue;

        return string.IsNullOrWhiteSpace(dtoValue) ? null : dtoValue;
    }

    private string? GetAvatarUrl(string contentType, string userId)
    {
        string? extension = contentType.Trim().ToLowerInvariant() switch
        {
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/avif" => ".avif",
            _ => null
        };

        if (extension is null)
            return null;

        string physicalPath;

        do
        {
            physicalPath = Path.Combine(_environment.WebRootPath,
                "Content", "Avatars",
                $"{Guid.NewGuid():N}_{userId}_{extension}");
        }
        while (System.IO.File.Exists(physicalPath));

        return Path.GetRelativePath(_environment.ContentRootPath, physicalPath);
    }
    public async Task<OneOf<bool, ServiceResultContext>> IsAdult(
    CancellationToken ct = default)
    {
        string? userId = GetUserID();
        if (userId is null)
            return false;

        var minimumBirthday = DateOnly.MinValue;
        var birthdayLimit = DateOnly
            .FromDateTime(DateTime.UtcNow)
            .AddYears(-18);

        return await _dbHelperService.TryFunction(
            async cancel => await _context.UserInfos
                .AnyAsync(info =>
                    info.UserId == userId &&
                    info.Birthday > minimumBirthday &&
                    info.Birthday <= birthdayLimit,
                    cancel),
            ServiceResult.DBError, ct: ct);
    }
}
