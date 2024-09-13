using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SNEStorage.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SNEStorage.Controllers
{
    [ApiController]
    [Route("api/account")]
    public class Account : ControllerBase
    {
        public UserManager<IdentityUser> UserManager { get; }
        public IConfiguration Configuration { get; }
        public SignInManager<IdentityUser> SignInManager { get; }

        public Account(UserManager<IdentityUser> userManager, 
            IConfiguration configuration,
            SignInManager<IdentityUser> signInManager)
        {
            UserManager = userManager;
            Configuration = configuration;
            SignInManager = signInManager;
        }
        [HttpPost("register")]
        public async Task<ActionResult<AuthenticationResponse>> Register(Credentials credentials)
        {
            IdentityUser user = new(credentials.User)
            {
                Email = credentials.Email
            };
            var result = await UserManager.CreateAsync(user, credentials.Password);
            if (!result.Succeeded)
                return BadRequest(result.Errors);
            return buildToken(credentials.User);
        }
        [HttpPost("login")]
        public async Task<ActionResult<AuthenticationResponse>> LogIn(LoginCredentials credentials)
        {
            var result = await SignInManager.PasswordSignInAsync(credentials.User, credentials.Password,
                false, false);
            if (!result.Succeeded)
                return BadRequest("Incorrect Log In");
            return buildToken(credentials.User);
        }
        private AuthenticationResponse buildToken(string user)
        {
            List<Claim> claims = [new("user", user)];
            SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(Configuration["JWTKey"]!));
            SigningCredentials creds = new(key, SecurityAlgorithms.HmacSha256);
            DateTime expiration = DateTime.UtcNow.AddDays(1);
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
