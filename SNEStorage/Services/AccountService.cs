using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SNEStorage.DTOs;
using SNEStorage.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SNEStorage.Services
{
    public class AccountService
    {
        public UserManager<ApplicationUser> UserManager { get; }
        public SignInManager<ApplicationUser> SignInManager { get; }
        public IConfiguration Configuration { get; }
        public SnestorageContext Context { get; }
        public FileService FileService { get; }
        public IHttpContextAccessor HttpContextAccessor { get; }

        public AccountService(UserManager<ApplicationUser> userManager,
                              IConfiguration configuration,
                              SignInManager<ApplicationUser> signInManager,
                              SnestorageContext context,
                              FileService fileService,
                              IHttpContextAccessor httpContextAccessor)
        {
            UserManager = userManager;
            Configuration = configuration;
            SignInManager = signInManager;
            Context = context;
            FileService = fileService;
            HttpContextAccessor = httpContextAccessor;
        }
        public async Task<ActionResult<AuthenticationResponse>?> Register(RegisterInfo registerInfo)
        {
            var user = new ApplicationUser { UserName = registerInfo.User, Email = registerInfo.Email };
            var result = await UserManager.CreateAsync(user, registerInfo.Password);
            if (!result.Succeeded) return null;

            var res = await BuildToken(registerInfo.User);
            if (res == null) return null;

            var avatar = registerInfo.Avatar;
            var path = $"Content/Avatars/{user.Id}_{user.UserName}{Path.GetExtension(avatar.FileName)}";
            var res2 = await FileService.CreateAsync(avatar, path, CancellationToken.None);
            if (res2 == null) return null;                 // antes chequeabas 'res' por error

            var info = new UserInfo
            {
                UserId = user.Id,
                UsernameColor = registerInfo.UsernameColor,
                AvatarId = res2.Id,
                Birthday = registerInfo.Birthday,
                TimeZoneId = registerInfo.TimeZoneId,
                EmailVisibilityId = registerInfo.EmailVisibilityId
            };
            Context.Add(info);
            await Context.SaveChangesAsync();

            return res;
        }

        public async Task<ActionResult<AuthenticationResponse>?> Login(LoginCredentials credentials)
        {
            var result = await SignInManager.PasswordSignInAsync(credentials.User!, credentials.Password!, false, false);
            if (!result.Succeeded) return null;
            return await BuildToken(credentials.User!);
        }

        public async Task<ActionResult<AuthenticationResponse>?> RefreshToken()
        {
            var user = HttpContextAccessor.HttpContext!.User.FindFirstValue("user");
            if (string.IsNullOrEmpty(user)) return null;
            return await BuildToken(user);
        }

        public async Task<AuthenticationResponse?> BuildToken(string user)
        {
            var u = await UserManager.FindByNameAsync(user);
            if (u == null) return null;

            var claims = new List<Claim> { new("user", user) };
            claims.AddRange(await UserManager.GetClaimsAsync(u));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Configuration["JWTKey"]!));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var exp = DateTime.UtcNow.AddMinutes(30);

            var jwt = new JwtSecurityToken(claims: claims, expires: exp, signingCredentials: creds);
            return new AuthenticationResponse { Token = new JwtSecurityTokenHandler().WriteToken(jwt), ExpireTime = exp };
        }
        public async Task<ActionResult<bool>> AddRole(EditRole editRole)
        {
            var user = await UserManager.FindByNameAsync(editRole.Username);
            if (user == null)
                return false;
            var claims = await UserManager.GetClaimsAsync(user);
            if (claims.Contains(new(editRole.RoleName, "1")))
                return false;
            await UserManager.AddClaimAsync(user, new(editRole.RoleName, "1"));
            return true;
        }
        public async Task<ActionResult<bool>> RemoveRole(EditRole editRole)
        {
            var user = await UserManager.FindByNameAsync(editRole.Username);
            if (user == null)
                return false;
            var claims = await UserManager.GetClaimsAsync(user);
            if (!claims.Contains(new(editRole.RoleName, "1")))
                return false;
            await UserManager.RemoveClaimAsync(user, new(editRole.RoleName, "1"));
            return true;
        }
        
    }
}
