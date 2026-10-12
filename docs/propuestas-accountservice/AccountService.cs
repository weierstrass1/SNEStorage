// Propuesta completa. Requiere la sobrecarga de DBHelperService indicada
// en README.md y adaptar los controladores a las firmas nuevas.
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.Models;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SNEStorage.Services;

public enum AccountServiceResult
{
    UserNotFound,
    InvalidToken,
    UserAlreadyExists,
    CancelledToken,
    AvatarLoadError,
    UserCreationError,
    TokenCreationError,
    DBError,
    Success,
    Error,

    Unauthenticated,
    InvalidInput,
    InvalidCredentials,
    EmailNotConfirmed,
    LockedOut,
    RoleAlreadyAssigned,
    RoleNotAssigned,
    RoleUpdateError,
    LogoutError,
    CacheError
}

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
    ILogger<AccountService> logger)
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;
    private readonly IWebHostEnvironment _environment = environment;
    private readonly IConfiguration _configuration = configuration;
    private readonly SignInManager<ApplicationUser> _signInManager = signInManager;
    private readonly SnestorageContext _context = context;
    private readonly FileService _fileService = fileService;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;
    private readonly EmailService _emailService = emailService;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly ILogger<AccountService> _logger = logger;

    public async Task<OneOf<RegisterResponseDTO, AccountServiceResult, FileServiceResult>> Register(
        RegisterInfo dto, CancellationToken ct = default)
    {
        var user = new ApplicationUser { UserName = dto.User, Email = dto.Email };
        var batchPath = await _fileService.BeginBatch(ct);
        if (batchPath == null)
            return ct.IsCancellationRequested
                ? AccountServiceResult.CancelledToken
                : AccountServiceResult.AvatarLoadError;

        var prepared = await _fileService.PrepareFile(
            dto.Avatar, GetAvatarUrl(dto.Avatar.FileName, user.Id, dto.User),
            batchPath, ct);

        if (!prepared.TryPickT0(out var avatar, out var fileError))
            return fileError;

        var saved = await PersistRegistration(user, dto, avatar, batchPath, ct);
        if (saved != AccountServiceResult.Success)
            return saved;

        await SendConfirmationAfterRegistration(user, ct);

        return new RegisterResponseDTO
        {
            UserId = user.Id,
            RequiresEmailConfirmation = true
        };
    }

    private async Task<AccountServiceResult> PersistRegistration(
        ApplicationUser user,
        RegisterInfo dto,
        Models.File avatar,
        string batchPath,
        CancellationToken ct)
    {
        bool committed = false;

        var result = await RunAccount(async cancel =>
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync(cancel);

            var created = await _userManager.CreateAsync(user, dto.Password);
            if (!created.Succeeded)
            {
                // Dispose revierte esta transacción sin commit.
                return created.Errors.Any(error =>
                    error.Code is "DuplicateUserName" or "DuplicateEmail")
                    ? AccountServiceResult.UserAlreadyExists
                    : AccountServiceResult.UserCreationError;
            }

            _context.UserInfos.Add(new UserInfo
            {
                UserId = user.Id,
                UsernameColor = dto.UsernameColor,
                Birthday = dto.Birthday,
                TimeZoneId = dto.TimeZoneId,
                EmailVisibilityId = dto.EmailVisibilityId,
                Avatar = avatar
            });

            await _context.SaveChangesAsync(cancel);
            await transaction.CommitAsync(cancel);
            committed = true;
            return AccountServiceResult.Success;
        }, ct);

        if (!committed)
        {
            await _fileService.BatchRollback(batchPath, CancellationToken.None);
            return result;
        }

        // Si el commit terminó, no borrar sus archivos por un fallo posterior.
        _fileService.BatchComplete(batchPath);

        if (result != AccountServiceResult.Success)
            _logger.LogWarning("Registration committed but transaction cleanup failed for user {UserId}.", user.Id);

        return AccountServiceResult.Success;
    }

    private async Task SendConfirmationAfterRegistration(
        ApplicationUser user, CancellationToken ct)
    {
        var sent = await _dbHelperService.TryFunction<EmailServiceResult>(async cancel =>
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));

            return await _emailService.Confirmation(
                user.UserName!, user.Id, user.Email!, encoded, cancel);
        }, _ => EmailServiceResult.EmailConnectionError, ct);

        if (sent != EmailServiceResult.Success)
            _logger.LogWarning("Confirmation email failed for user {UserId}: {Result}.",
                user.Id, sent);
    }

    public async Task<OneOf<AuthenticationResponse, AccountServiceResult>> Login(
        LoginCredentials credentials, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(credentials.User) ||
            string.IsNullOrEmpty(credentials.Password))
            return AccountServiceResult.InvalidInput;

        var userResult = await QueryAccount<ApplicationUser?>(
            _ => _userManager.FindByNameAsync(credentials.User), ct);

        if (!userResult.TryPickT0(out var user, out var error))
            return error;

        if (user == null)
            return AccountServiceResult.InvalidCredentials;

        var signInResult = await QueryAccount<SignInResult>(
            _ => _signInManager.CheckPasswordSignInAsync(
                user, credentials.Password, lockoutOnFailure: true), ct);

        if (!signInResult.TryPickT0(out var signIn, out error))
            return error;

        if (signIn.IsLockedOut)
            return AccountServiceResult.LockedOut;

        if (!signIn.Succeeded)
        {
            // RequireConfirmedEmail sigue configurado en Identity.
            // IsNotAllowed puede tener otras causas si añades más políticas.
            return signIn.IsNotAllowed && !user.EmailConfirmed
                ? AccountServiceResult.EmailNotConfirmed
                : AccountServiceResult.InvalidCredentials;
        }

        return await BuildToken(user, ct);
    }

    public Task<AccountServiceResult> Logout(CancellationToken ct = default)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
            return Task.FromResult(AccountServiceResult.Unauthenticated);

        return RunAccount(async _ =>
        {
            await _signInManager.SignOutAsync();
            context.Session.Remove("Token");
            return AccountServiceResult.Success;
        }, ct, AccountServiceResult.LogoutError);
    }

    public Task<OneOf<UserInfo, AccountServiceResult>> GetUserInfo(
        CancellationToken ct)
    {
        var context = _httpContextAccessor.HttpContext;
        var userId = GetUserID();

        if (context?.User.Identity?.IsAuthenticated != true ||
            string.IsNullOrWhiteSpace(userId))
        {
            return Task.FromResult<OneOf<UserInfo, AccountServiceResult>>(
                AccountServiceResult.Unauthenticated);
        }

        return GetUserInfo(userId, ct);
    }

    public async Task<OneOf<UserInfo, AccountServiceResult>> GetUserInfo(
        string userId, CancellationToken ct)
    {
        var result = await QueryAccount<UserInfo?>(cancel => _context.UserInfos
            .AsNoTracking()
            .Include(info => info.TimeZone)
            .Include(info => info.EmailVisibility)
            .Include(info => info.Avatar)
            .Include(info => info.User)
            .FirstOrDefaultAsync(info => info.UserId == userId, cancel), ct);

        if (!result.TryPickT0(out var info, out var error))
            return error;

        if (info == null)
            return AccountServiceResult.UserNotFound;

        info.UserSummary = UserNameDTO.FromUserInfo(info, this);
        return info;
    }

    public async Task<OneOf<AuthenticationResponse, AccountServiceResult>> RefreshToken(
        CancellationToken ct = default)
    {
        var result = await GetUserResult(ct);
        if (!result.TryPickT0(out var user, out var error))
            return error;

        return await BuildToken(user, ct);
    }

    public async Task<OneOf<AuthenticationResponse, AccountServiceResult>> BuildToken(
        string username, CancellationToken ct = default)
    {
        var result = await QueryAccount<ApplicationUser?>(
            _ => _userManager.FindByNameAsync(username), ct);

        if (!result.TryPickT0(out var user, out var error))
            return error;

        if (user == null)
            return AccountServiceResult.UserNotFound;

        return await BuildToken(user, ct);
    }

    private async Task<OneOf<AuthenticationResponse, AccountServiceResult>> BuildToken(
        ApplicationUser user, CancellationToken ct)
    {
        if (!user.EmailConfirmed)
            return AccountServiceResult.EmailNotConfirmed;

        var lockoutResult = await QueryAccount<bool>(
            _ => _userManager.IsLockedOutAsync(user), ct);

        if (!lockoutResult.TryPickT0(out var lockedOut, out var error))
            return error;

        if (lockedOut)
            return AccountServiceResult.LockedOut;

        var claimsResult = await QueryAccount<IList<Claim>>(
            _ => _userManager.GetClaimsAsync(user), ct);

        if (!claimsResult.TryPickT0(out var storedClaims, out error))
            return error;

        return await QueryAccount<AuthenticationResponse>(_ =>
        {
            var claims = new List<Claim>
            {
                new("user", user.UserName!),
                new(ClaimTypes.NameIdentifier, user.Id),
                new(ClaimTypes.Name, user.UserName!)
            };
            claims.AddRange(storedClaims);

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(_configuration["JWTKey"]!));
            var credentials = new SigningCredentials(
                key, SecurityAlgorithms.HmacSha256);
            var expiration = DateTime.UtcNow.AddMinutes(30);
            var jwt = new JwtSecurityToken(
                claims: claims, expires: expiration,
                signingCredentials: credentials);

            return Task.FromResult(new AuthenticationResponse
            {
                Token = new JwtSecurityTokenHandler().WriteToken(jwt),
                ExpireTime = expiration
            });
        }, ct, AccountServiceResult.TokenCreationError);
    }

    public Task<AccountServiceResult> AddRole(
        EditRole dto, CancellationToken ct = default)
    {
        return RunAccount(async _ =>
        {
            var user = await _userManager.FindByNameAsync(dto.Username);
            if (user == null)
                return AccountServiceResult.UserNotFound;

            var claims = await _userManager.GetClaimsAsync(user);
            if (claims.Any(c => c.Type == dto.RoleName && c.Value == "1"))
                return AccountServiceResult.RoleAlreadyAssigned;

            var result = await _userManager.AddClaimAsync(
                user, new Claim(dto.RoleName, "1"));

            return result.Succeeded
                ? AccountServiceResult.Success
                : AccountServiceResult.RoleUpdateError;
        }, ct);
    }

    public Task<AccountServiceResult> RemoveRole(
        EditRole dto, CancellationToken ct = default)
    {
        return RunAccount(async _ =>
        {
            var user = await _userManager.FindByNameAsync(dto.Username);
            if (user == null)
                return AccountServiceResult.UserNotFound;

            var claims = await _userManager.GetClaimsAsync(user);
            var claim = claims.FirstOrDefault(c =>
                c.Type == dto.RoleName && c.Value == "1");

            if (claim == null)
                return AccountServiceResult.RoleNotAssigned;

            var result = await _userManager.RemoveClaimAsync(user, claim);
            return result.Succeeded
                ? AccountServiceResult.Success
                : AccountServiceResult.RoleUpdateError;
        }, ct);
    }

    public async Task<OneOf<AccountServiceResult, FileServiceResult>> EditUserInfo(
        EditProfileDTO dto, CancellationToken ct)
    {
        var principal = _httpContextAccessor.HttpContext?.User;
        var userId = GetUserID();
        var username = principal?.FindFirstValue(ClaimTypes.Name);

        if (principal?.Identity?.IsAuthenticated != true ||
            string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(username))
            return AccountServiceResult.Unauthenticated;

        var infoResult = await QueryAccount<UserInfo?>(cancel => _context.UserInfos
            .Include(info => info.Avatar)
            .FirstOrDefaultAsync(info => info.UserId == userId, cancel), ct);

        if (!infoResult.TryPickT0(out var info, out var error))
            return error;
        if (info == null)
            return AccountServiceResult.UserNotFound;

        var previousAvatar = info.Avatar;
        Models.File? newAvatar = null;
        string? batchPath = null;

        if (dto.File != null)
        {
            if (string.IsNullOrWhiteSpace(dto.File.FileName))
                return AccountServiceResult.InvalidInput;

            batchPath = await _fileService.BeginBatch(ct);
            if (batchPath == null)
                return ct.IsCancellationRequested
                    ? AccountServiceResult.CancelledToken
                    : AccountServiceResult.AvatarLoadError;

            // El límite específico de avatar/contexto sigue siendo una
            // responsabilidad de la validación de archivos, no del enum.
            var prepared = await _fileService.PrepareFile(
                dto.File, GetAvatarUrl(dto.File.FileName, userId, username),
                batchPath, ct);

            if (!prepared.TryPickT0(out newAvatar, out var fileError))
                return fileError;

            info.Avatar = newAvatar;
        }

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
        info.NintendoNetwork = GetSocialNetwork(dto.NintendoNetwork, info.NintendoNetwork);
        info.TimeZoneId = dto.TimeZoneId ?? info.TimeZoneId;
        info.EmailVisibilityId = dto.EmailVisibilityId ?? info.EmailVisibilityId;

        var saved = await QueryAccount<int>(
            cancel => _context.SaveChangesAsync(cancel), ct);

        if (saved.TryPickT1(out error, out _))
        {
            if (batchPath != null)
                await _fileService.BatchRollback(batchPath, CancellationToken.None);
            else
                _context.ChangeTracker.Clear();

            return error;
        }

        if (batchPath != null)
            _fileService.BatchComplete(batchPath);

        if (newAvatar != null && previousAvatar != null)
            await CleanupPreviousAvatar(previousAvatar, ct);

        return AccountServiceResult.Success;
    }

    private async Task CleanupPreviousAvatar(Models.File previousAvatar, CancellationToken ct)
    {
        bool cleaned = await _dbHelperService.TryFunction(async cancel =>
        {
            await _context.Files
                .Where(file => file.Id == previousAvatar.Id)
                .ExecuteDeleteAsync(cancel);

            System.IO.File.Delete(Path.Combine(
                _environment.ContentRootPath, previousAvatar.URL));
        }, ct: ct);

        if (!cleaned)
            _logger.LogWarning("Previous avatar cleanup failed for file {FileId}.",
                previousAvatar.Id);
    }

    public string? GetUserID()
    {
        return _httpContextAccessor.HttpContext?.User
            .FindFirstValue(ClaimTypes.NameIdentifier);
    }

    public async Task<AccountServiceResult> ConfirmUser(
        string userId, string token64, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(token64))
            return AccountServiceResult.InvalidToken;

        string? token = _dbHelperService.TryFunction(() =>
            Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(token64)));

        if (token == null)
            return AccountServiceResult.InvalidToken;

        return await RunAccount(async _ =>
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return AccountServiceResult.UserNotFound;

            var result = await _userManager.ConfirmEmailAsync(user, token);
            return result.Succeeded
                ? AccountServiceResult.Success
                : AccountServiceResult.InvalidToken;
        }, ct);
    }

    public async Task<OneOf<ApplicationUser, AccountServiceResult>> GetUserResult(
        CancellationToken ct = default)
    {
        var userId = GetUserID();
        if (string.IsNullOrWhiteSpace(userId))
            return AccountServiceResult.Unauthenticated;

        var result = await QueryAccount<ApplicationUser?>(
            async cancel => await _context.Users.FindAsync([userId], cancel), ct);

        if (!result.TryPickT0(out var user, out var error))
            return error;

        return user == null
            ? AccountServiceResult.UserNotFound
            : user;
    }

    // Adaptador temporal para los consumidores actuales.
    public async Task<ApplicationUser?> GetUser(CancellationToken ct = default)
    {
        var result = await GetUserResult(ct);
        return result.TryPickT0(out var user, out _) ? user : null;
    }

    private static AccountServiceResult OperationError(
        Exception exception,
        CancellationToken ct,
        AccountServiceResult fallback)
    {
        return exception is OperationCanceledException && ct.IsCancellationRequested
            ? AccountServiceResult.CancelledToken
            : fallback;
    }

    private Task<AccountServiceResult> RunAccount(
        Func<CancellationToken, Task<AccountServiceResult>> operation,
        CancellationToken ct,
        AccountServiceResult fallback = AccountServiceResult.DBError)
    {
        return _dbHelperService.TryFunction<AccountServiceResult>(
            operation,
            exception => OperationError(exception, ct, fallback),
            ct);
    }

    private Task<OneOf<T, AccountServiceResult>> QueryAccount<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken ct,
        AccountServiceResult fallback = AccountServiceResult.DBError)
    {
        return _dbHelperService.TryFunction<OneOf<T, AccountServiceResult>>(
            async cancel => await operation(cancel),
            exception => OperationError(exception, ct, fallback),
            ct);
    }

    public bool IsModeratorOrAdmin()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        return user?.Identity?.IsAuthenticated == true &&
            (user.HasClaim("Admin", "1") || user.HasClaim("Moderator", "1"));
    }

    private static string? GetSocialNetwork(string? dtoValue, string? previousValue)
    {
        if (dtoValue == null)
            return previousValue;

        return string.IsNullOrWhiteSpace(dtoValue) ? null : dtoValue;
    }

    private string GetAvatarUrl(string filename, string userId, string username)
    {
        string physicalPath;
        do
        {
            physicalPath = Path.Combine(
                _environment.WebRootPath, "Content", "Avatars",
                $"{Guid.NewGuid():N}_{userId}_{username}{Path.GetExtension(filename)}");
        }
        while (System.IO.File.Exists(physicalPath));

        return Path.GetRelativePath(_environment.ContentRootPath, physicalPath)
            .Replace('\\', '/');
    }
}
