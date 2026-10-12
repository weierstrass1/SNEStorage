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
public enum AccountServiceResult
{
    UserNotFound,
    PasswordChangeError,
    InvalidEmail,
    InvalidPassword,
    InvalidToken,
    UserAlreadyExists,
    CancelledToken,
    AvatarLoadError,
    UserCreationError,
    TokenCreationError,
    DBError,
    Success,
    Error
}
public class AccountService(UserManager<ApplicationUser> userManager,
                        IWebHostEnvironment environment,
                      IConfiguration configuration,
                      SignInManager<ApplicationUser> signInManager,
                      SnestorageContext context,
                      FileService fileService,
                      IHttpContextAccessor httpContextAccessor,
                      EmailService emailService,
                      DBHelperService dbHelperService)
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
    public async Task<AccountServiceResult> ChangePassword(ChangePasswordDTO dto, 
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(dto.UserId) ||
            string.IsNullOrWhiteSpace(dto.Token) ||
            string.IsNullOrWhiteSpace(dto.NewPassword) ||
            dto.NewPassword != dto.ConfirmPassword)
            return AccountServiceResult.InvalidPassword;

        var token = _dbHelperService.TryFunction(() => Encoding.UTF8
            .GetString(WebEncoders.Base64UrlDecode(dto.Token)));

        if (string.IsNullOrEmpty(token))
            return AccountServiceResult.InvalidToken;

        var userRes = await _dbHelperService.TryFunction(
            async _ => await _userManager.FindByIdAsync(dto.UserId),
            AccountServiceResult.DBError, ct: ct);

        if (!userRes.TryPickT0(out var user, out var error))
            return error;

        if (user == null)
            return AccountServiceResult.UserNotFound;

        var resetRes = await _dbHelperService.TryFunction(
            async _ => await _userManager.ResetPasswordAsync(user, token, dto.NewPassword),
            AccountServiceResult.DBError, ct: ct);

        if (!resetRes.TryPickT0(out var result, out error))
            return error;

        if (result.Succeeded)
            return AccountServiceResult.Success;

        return result.Errors.Any(e =>
            e.Code == nameof(IdentityErrorDescriber.InvalidToken)) ?
                AccountServiceResult.InvalidToken :
                AccountServiceResult.PasswordChangeError;
    }
    public async Task<OneOf<AccountServiceResult, EmailServiceResult>> RequestPasswordReset(string email, 
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return AccountServiceResult.InvalidEmail;

        var userRes = await _dbHelperService.TryFunction(
            async _ => await _userManager.FindByEmailAsync(email.Trim()),
            AccountServiceResult.DBError, ct: ct);

        if (!userRes.TryPickT0(out var user, out var error))
            return error;

        if (user == null)
            return AccountServiceResult.Success;

        var tokenRes = await _dbHelperService.TryFunction(
            async _ => await _userManager.GeneratePasswordResetTokenAsync(user),
            AccountServiceResult.TokenCreationError, ct: ct);

        if (!tokenRes.TryPickT0(out var token, out error))
            return error;

        var encodedToken = WebEncoders.Base64UrlEncode(
            Encoding.UTF8.GetBytes(token));

        var emailResult = await _emailService.PasswordReset(user.UserName!, user.Id, 
            user.Email!, encodedToken, ct);

        if (emailResult != EmailServiceResult.Success)
            return emailResult;

        return AccountServiceResult.Success;
    }
    public async Task<OneOf<AccountServiceResult, EmailServiceResult>> RequestEmailConfirmation(
    string email, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return AccountServiceResult.InvalidEmail;

        var userRes = await _dbHelperService.TryFunction(
            async _ => await _userManager.FindByEmailAsync(email.Trim()),
            AccountServiceResult.DBError, ct: ct);
        if (!userRes.TryPickT0(out var user, out var error))
            return error;
        if (user == null || user.EmailConfirmed)
            return AccountServiceResult.Success;

        var tokenRes = await _dbHelperService.TryFunction(
            async _ => await _userManager.GenerateEmailConfirmationTokenAsync(user),
            AccountServiceResult.TokenCreationError, ct: ct);
        if (!tokenRes.TryPickT0(out var token, out error))
            return error;

        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var emailResult = await _emailService.Confirmation(
            user.UserName!, user.Id, user.Email!, encodedToken, ct);
        if (emailResult != EmailServiceResult.Success)
            return emailResult;

        return AccountServiceResult.Success;
    }
    public async Task<OneOf<RegisterResponseDTO, AccountServiceResult, FileServiceResult>> Register(RegisterInfo registerInfo, CancellationToken ct = default)
    {
        var user = new ApplicationUser { UserName = registerInfo.User, Email = registerInfo.Email };

        var avatar = registerInfo.Avatar;

        var path = GetAvatarUrl(avatar.ContentType, user.Id, user.UserName);
        string? batchPath = await _fileService.BeginBatch(ct);
        if (batchPath == null)
            return AccountServiceResult.AvatarLoadError;
        var res2Res = await _fileService.PrepareFile(FileContext.Avatar, avatar, path, batchPath, ct);
        if (!res2Res.TryPickT0(out var res2, out var ferror))
            return ferror;

        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        var result = await _userManager.CreateAsync(user, registerInfo.Password);
        if (!result.Succeeded)
        {
            await transaction.RollbackAsync(ct);
            await _fileService.BatchRollback(batchPath, ct);
            return AccountServiceResult.UserCreationError;
        }

        var info = new UserInfo
        {
            UserId = user.Id,
            UsernameColor = registerInfo.UsernameColor,
            Birthday = registerInfo.Birthday,
            TimeZoneId = registerInfo.TimeZoneId,
            EmailVisibilityId = registerInfo.EmailVisibilityId,
            Avatar = res2
        };
        _context.Add(info);

        try
        {
            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            _fileService.BatchComplete(batchPath);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            await _fileService.BatchRollback(batchPath, ct);
            return AccountServiceResult.DBError;
        }

        bool confirmationEmailSent = false;
        var confirmationToken = await _dbHelperService.TryFunction(
            async _ => await _userManager.GenerateEmailConfirmationTokenAsync(user), ct: ct);

        if (!string.IsNullOrWhiteSpace(confirmationToken))
        {
            var encodedToken = WebEncoders.Base64UrlEncode(
                Encoding.UTF8.GetBytes(confirmationToken));
            var emailResult = await _emailService.Confirmation(
                user.UserName!, user.Id, user.Email!, encodedToken, ct);
            confirmationEmailSent = emailResult == EmailServiceResult.Success;
        }

        return new RegisterResponseDTO
        {
            UserId = user.Id,
            ConfirmationEmailSent = confirmationEmailSent
        };
    }
    public async Task<AuthenticationResponse?> Login(LoginCredentials credentials)
    {
        var result = await _signInManager.PasswordSignInAsync(
            credentials.User!,
            credentials.Password!,
            isPersistent: false,
            lockoutOnFailure: true);
        if (!result.Succeeded) return null;
        return await BuildToken(credentials.User!);
    }
    public async Task<bool> Logout()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context == null)
            return false;

        context.Session.Remove("Token");

        await _signInManager.SignOutAsync();
        return true;
    }
    public async Task<OneOf<UserInfo, AccountServiceResult>> GetUserInfo(CancellationToken cancellationToken)
    {
        var userId = _httpContextAccessor.HttpContext?
            .User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrWhiteSpace(userId))
            return AccountServiceResult.UserNotFound;

        return await GetUserInfo(userId, cancellationToken);
    }
    public async Task<OneOf<UserInfo, AccountServiceResult>> GetUserInfo(string userId, CancellationToken ct)
    {
        try
        {
            var user = await _context.UserInfos
                .AsNoTracking()
                .Include(info => info.TimeZone)
                .Include(info => info.EmailVisibility)
                .Include(info => info.Avatar)
                .Include(info => info.User)
                .FirstOrDefaultAsync(info => info.UserId == userId, ct);
            if(user == null)
                return AccountServiceResult.UserNotFound;
            user.UserSummary = UserNameDTO.FromUserInfo(user, this);
            return user;
        }
        catch (OperationCanceledException)
        when (ct.IsCancellationRequested)
        {
            return AccountServiceResult.CancelledToken;
        }
        catch
        {
            return AccountServiceResult.Error;
        }
    }
    public async Task<AuthenticationResponse?> RefreshToken()
    {
        var user = _httpContextAccessor.HttpContext!.User.FindFirstValue("user");
        if (string.IsNullOrEmpty(user)) return null;
        return await BuildToken(user);
    }

    public async Task<AuthenticationResponse?> BuildToken(string user)
    {
        var u = await _userManager.FindByNameAsync(user);

        if (u is null || !await _userManager.IsEmailConfirmedAsync(u))
            return null;
        if (string.IsNullOrWhiteSpace(u.SecurityStamp))
            return null;

        var claims = new List<Claim> {
            new("user", user),
            new(ClaimTypes.NameIdentifier, u.Id),
            new(ClaimTypes.Name, u.UserName!),
            new("security_stamp", u.SecurityStamp)
        };
        claims.AddRange(await _userManager.GetClaimsAsync(u));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JWTKey"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var exp = DateTime.UtcNow.AddMinutes(30);

        var jwt = new JwtSecurityToken(claims: claims, expires: exp, signingCredentials: creds);
        return new AuthenticationResponse { Token = new JwtSecurityTokenHandler().WriteToken(jwt), ExpireTime = exp };
    }
    public async Task<bool> AddRole(EditRole editRole)
    {
        var user = await _userManager.FindByNameAsync(editRole.Username);
        if (user == null)
            return false;
        var claims = await _userManager.GetClaimsAsync(user);
        if (claims.Any(c => c.Type == editRole.RoleName && c.Value == "1"))
            return false;

        var result = await _userManager
            .AddClaimAsync(user, new Claim(editRole.RoleName, "1"));

        return result.Succeeded;
    }
    public async Task<bool> RemoveRole(EditRole editRole)
    {
        var user = await _userManager.FindByNameAsync(editRole.Username);
        if (user == null)
            return false;
        var claims = await _userManager.GetClaimsAsync(user);
        var claim = claims
            .FirstOrDefault(c => c.Type == editRole.RoleName && c.Value == "1");

        if (claim is null)
            return false;

        var result = await _userManager.RemoveClaimAsync(user, claim);

        return result.Succeeded;
    }
    public async Task<bool> EditUserInfo(EditProfileDTO dto, CancellationToken ct)
    {
        var user = _httpContextAccessor.HttpContext!.User.FindFirstValue(ClaimTypes.Name);
        if (user == null)
            return false;
        string? userId = GetUserID();
        if (userId == null)
            return false;

        var info = await _context.UserInfos
            .Include(ui => ui.Avatar)
            .FirstOrDefaultAsync(ui => ui.UserId == userId, ct);

        if (info == null)
            return false;

        var previousAvatar = info.Avatar;

        Models.File? file = null;

        string? batchPath = null;

        if (dto.File != null && !string.IsNullOrWhiteSpace(dto.File.FileName))
        {
            var path = GetAvatarUrl(dto.File.ContentType, userId, user);
            batchPath = await _fileService.BeginBatch(ct);
            if (batchPath == null)
                return false;
            var fileRes = await _fileService.PrepareFile(FileContext.Avatar, dto.File, path, batchPath, ct);
            if (!fileRes.TryPickT0(out file, out _))
                return false;
            info.Avatar = file;
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

        try
        {
            await _context.SaveChangesAsync(ct);
            if (batchPath != null)
                _fileService.BatchComplete(batchPath);
        }
        catch
        {
            if (batchPath != null)
                await _fileService.BatchRollback(batchPath, ct);
            else
                _context.ChangeTracker.Clear();
            return false;
        }
        if (file == null || previousAvatar == null)
            return true;

        bool cleaned = await _dbHelperService.TryFunction(async cancel =>
        {
            await _context.Files
                .Where(f => f.Id == previousAvatar.Id)
                .ExecuteDeleteAsync(cancel);

            var physicalPath = Path.Combine(
                _environment.ContentRootPath,
                previousAvatar.URL);

            System.IO.File.Delete(physicalPath);
        }, ct: ct);

        return true;
    }
    public string? GetUserID()
    {
        return _httpContextAccessor.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
    public async Task<AccountServiceResult> ConfirmUser(string userId, string token64)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return AccountServiceResult.UserNotFound;
        byte[] tokenbytes;
        try
        {
            tokenbytes = WebEncoders.Base64UrlDecode(token64);
        }
        catch
        {
            return AccountServiceResult.InvalidToken;
        }
        var token = Encoding.UTF8.GetString(tokenbytes);
        var result = await _userManager.ConfirmEmailAsync(user, token);

        return result.Succeeded ?
            AccountServiceResult.Success :
            AccountServiceResult.InvalidToken;
    }
    public bool IsModeratorOrAdmin()
    {
        var user = _httpContextAccessor.HttpContext?.User;

        return user?.Identity?.IsAuthenticated == true
            && (user.HasClaim("Admin", "1")
                || user.HasClaim("Moderator", "1"));
    }
    public async Task<ApplicationUser?> GetUser()
    {
        string? userId = GetUserID();
        if (userId == null)
            return null;

        return await _dbHelperService.TryFunction(
            async cancel => await _context.Users.FindAsync([userId], cancel));
    }
    public async Task<bool> ValidateJwt(string? userId, string? stamp,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId) ||
            string.IsNullOrWhiteSpace(stamp))
            return false;

        var currentStamp = await _dbHelperService.TryFunction(
            async cancel => await _context.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => u.SecurityStamp)
                .FirstOrDefaultAsync(cancel), ct: ct);

        return currentStamp != null &&
            string.Equals(currentStamp, stamp, StringComparison.Ordinal);
    }
    private static string? GetSocialNetwork(string? dtoValue, string? defValue)
    {
        if (dtoValue is null)
            return defValue;
        if(string.IsNullOrWhiteSpace(dtoValue))
            return null;
        return dtoValue;
    }
    private string? GetAvatarUrl(string contentType, string userId, string username)
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
            physicalPath = Path.Combine(
                _environment.WebRootPath, "Content", "Avatars",
                $"{Guid.NewGuid():N}_{userId}_{username}{extension}");
        }
        while (System.IO.File.Exists(physicalPath));

        return Path.GetRelativePath(
            _environment.ContentRootPath, physicalPath);
    }
}
