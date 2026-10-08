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
    public async Task<OneOf<RegisterResponseDTO, AccountServiceResult>> Register(RegisterInfo registerInfo, CancellationToken ct = default)
    {
        var user = new ApplicationUser { UserName = registerInfo.User, Email = registerInfo.Email };

        var avatar = registerInfo.Avatar;

        var path = GetAvatarUrl(avatar.FileName, user.Id, user.UserName);
        string? batchPath = await _fileService.BeginBatch(ct);
        if (batchPath == null)
            return AccountServiceResult.AvatarLoadError;
        Models.File? res2 = await _fileService.PrepareFile(avatar, path, batchPath, ct);
        if (res2 == null)
            return AccountServiceResult.AvatarLoadError;

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

        var confirmationToken = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        var encodedToken = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(confirmationToken));
        await _emailService.Confirmation(user.UserName, user.Id, user.Email, encodedToken, ct);

        return new RegisterResponseDTO
        {
            UserId = user.Id
        };
    }
    public async Task<AuthenticationResponse?> Login(LoginCredentials credentials)
    {
        var result = await _signInManager.PasswordSignInAsync(credentials.User!, credentials.Password!, false, false);
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

        var claims = new List<Claim> {
            new("user", user),
            new(ClaimTypes.NameIdentifier, u.Id),
            new(ClaimTypes.Name, u.UserName!)
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
            .FirstOrDefaultAsync(ui => ui.UserId == userId, ct);
        if (info == null)
            return false;

        Models.File? file = null;

        string? batchPath = null;

        if (dto.File != null && !string.IsNullOrWhiteSpace(dto.File.FileName))
        {
            var path = GetAvatarUrl(dto.File.FileName, userId, user);
            batchPath = await _fileService.BeginBatch(ct);
            if (batchPath == null)
                return false;
            file = await _fileService.PrepareFile(dto.File, path, batchPath, ct);
            if (file == null)
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
            return true;
        }
        catch
        {
            if (batchPath != null)
                await _fileService.BatchRollback(batchPath, ct);
            else
                _context.ChangeTracker.Clear();
            return false;
        }
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
    private static string? GetSocialNetwork(string? dtoValue, string? defValue)
    {
        if (dtoValue is null)
            return defValue;
        if(string.IsNullOrWhiteSpace(dtoValue))
            return null;
        return dtoValue;
    }
    private string GetAvatarUrl(string avatarUrl, string userId, string username)
    {
        string path;
        do
        {
            path = Path.GetRelativePath(_environment.ContentRootPath,
            Path.Combine(_environment.WebRootPath, "Content", "Avatars",
                $"{Guid.NewGuid():N}_{userId}_{username}{Path.GetExtension(avatarUrl)}"));
        }
        while (System.IO.File.Exists(path));
        return path;
    }
}
