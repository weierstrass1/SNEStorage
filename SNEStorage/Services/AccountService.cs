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
        public UserManager<IdentityUser> UserManager { get; }
        public IConfiguration Configuration { get; }
        public SignInManager<IdentityUser> SignInManager { get; }
        public SnestorageContext Context { get; }
        public FileService FileService { get; }
        public IHttpContextAccessor HttpContextAccessor { get; }

        public AccountService(UserManager<IdentityUser> userManager,
            IConfiguration configuration,
            SignInManager<IdentityUser> signInManager,
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
            IdentityUser user = new(registerInfo.User)
            {
                Email = registerInfo.Email
            };
            var result = await UserManager.CreateAsync(user, registerInfo.Password);
            if (!result.Succeeded)
                return null;
            var res = await BuildToken(registerInfo.User);
            if (res == null)
                return null;
            var avatar = registerInfo.Avatar;
            var res2 = await FileService.Create(avatar, $"Content/Avatars/{user.Id}_{user.UserName}{Path.GetExtension(avatar.FileName)}");
            if(res == null)
                return null;
            UserInfo info = new()
            {
                UserId = user.Id,
                UsernameColor = registerInfo.UsernameColor,
                AvatarId = res2.Value!.Id,
                Birthday = registerInfo.Birthday,
                TimeZoneId = registerInfo.TimeZoneId,
                EmailVisibilityId = registerInfo.EmailVisibilityId
            };
            Context.Add(info);
            Context.SaveChanges();
            return res;
        }
        public async Task<ActionResult<AuthenticationResponse>?> Login(LoginCredentials credentials)
        {
            var result = await SignInManager.PasswordSignInAsync(credentials.User!,
                credentials.Password!, false, false);
            if (!result.Succeeded)
                return null;
            var res = await BuildToken(credentials.User!);
            if (res == null)
                return null;
            return res;
        }
        public async Task<ActionResult<AuthenticationResponse>?> RefreshToken()
        {
            string user = HttpContextAccessor.HttpContext!.User.Claims
                            .FirstOrDefault(claim => claim.Type == "user")!
                            .Value;
            var res = await BuildToken(user);
            if (res == null)
                return null;
            return res;
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
        public async Task<AuthenticationResponse?> BuildToken(string user)
        {
            List<Claim> claims = [new("user", user)];
            var usr = await UserManager.FindByNameAsync(user);
            if (usr == null)
                return null;
            var usrClaims = await UserManager.GetClaimsAsync(usr);
            claims.AddRange(usrClaims);

            SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(Configuration["JWTKey"]!));
            SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);
            DateTime expiration = DateTime.UtcNow.AddMinutes(30);
            JwtSecurityToken token = new(
                claims: claims,
                expires: expiration,
                signingCredentials: creds);
            return new()
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                ExpireTime = expiration
            };
        }
    }
}
