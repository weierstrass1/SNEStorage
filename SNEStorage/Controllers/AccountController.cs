using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using SNEStorage.DTOs;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    public UserManager<IdentityUser> UserManager { get; }
    public IConfiguration Configuration { get; }
    public SignInManager<IdentityUser> SignInManager { get; }

    public AccountController(UserManager<IdentityUser> userManager, 
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
        var res = await buildToken(credentials.User);
        if (res == null)
            return BadRequest();
        return res;
    }
    [HttpPost("login")]
    public async Task<ActionResult<AuthenticationResponse>> Login(LoginCredentials credentials)
    {
        var result = await SignInManager.PasswordSignInAsync(credentials.User, credentials.Password,
            false, false);
        if (!result.Succeeded)
            return BadRequest("Incorrect Log In");
        var res = await buildToken(credentials.User);
        if (res == null)
            return BadRequest();
        return res;
    }
    [HttpGet("refreshToken")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
    public async Task<ActionResult<AuthenticationResponse>> RefreshToken()
    {
        string user = HttpContext.User.Claims
                        .FirstOrDefault(claim => claim.Type == "user")!
                        .Value;
        var res = await buildToken(user);
        if (res == null)
            return BadRequest();
        return res;
    }
    [HttpPost("AddRole")]
    [Authorize(AuthenticationSchemes =JwtBearerDefaults.AuthenticationScheme, Policy = "Admin")]
    public async Task<ActionResult> AddRole(EditRole editRole)
    {
        var user = await UserManager.FindByNameAsync(editRole.Username);
        if (user == null)
            return BadRequest();
        var claims = await UserManager.GetClaimsAsync(user);
        if (claims.Contains(new(editRole.RoleName, "1")))
            return BadRequest();
        await UserManager.AddClaimAsync(user, new(editRole.RoleName, "1"));
        return NoContent();
    }
    [HttpPost("RemoveRole")]
    [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Policy = "Admin")]
    public async Task<ActionResult> RemoveRole(EditRole editRole)
    {
        var user = await UserManager.FindByNameAsync(editRole.Username);
        if (user == null)
            return BadRequest();
        var claims = await UserManager.GetClaimsAsync(user);
        if (!claims.Contains(new(editRole.RoleName, "1")))
            return BadRequest();
        await UserManager.RemoveClaimAsync(user, new(editRole.RoleName, "1"));
        return NoContent();
    }
    private async Task<AuthenticationResponse?> buildToken(string user)
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
