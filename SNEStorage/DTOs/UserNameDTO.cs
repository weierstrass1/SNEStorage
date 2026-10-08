using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorage.DTOs;
public class UserNameDTO
{
    public required string Id { get; set; }
    public required string Username { get; set; }
    public string? Email { get; set; }
    public string? UsernameColor { get; set; }
    public FileResponse? Avatar { get; set; }
    public static void FillUserSummaries<T>(Action<T, UserNameDTO> set, 
        Func<IEnumerable<(T, ApplicationUser)>> usersFunc,
        AccountService? accountService = null)
    {
        foreach((T target, ApplicationUser source)  in usersFunc())
        {
            set(target, FromApplicationUser(source, accountService));
        }
    }
    public static void FillUserSummaries<T>(Action<T, UserNameDTO> set, 
        Func<IEnumerable<(T, UserInfo)>> usersFunc,
        AccountService? accountService = null)
    {
        foreach ((T target, UserInfo source) in usersFunc())
        {
            set(target, FromUserInfo(source, accountService));
        }
    }
    public static UserNameDTO FromApplicationUser(ApplicationUser user, AccountService? accountService = null)
    {
        if (user.UserInfo != null)
            return FromUserInfo(user.UserInfo, accountService);
        return new()
        {
            Id = user.Id,
            Username = user.UserName!
        };
    }
    public static async Task<UserNameDTO?> FromUser(AccountService accountService)
    {
        var user = await accountService.GetUser();
        return user != null ?
            FromApplicationUser(user, accountService) :
            null;
    }
    public static UserNameDTO FromUserInfo(UserInfo user, AccountService? accountService = null)
    {
        UserNameDTO result = new()
        {
            Id = user.UserId,
            Username = user.User!.UserName!,
            UsernameColor = user.UsernameColor,
            Email = user.EmailVisibilityId == 1 ||
                    accountService?.GetUserID() == user.UserId?
                user.User.Email :
                null
        };
        var file = user.Avatar;
        if (file == null)
            return result;

        result.Avatar = new()
        {
            FileTypeId = file.FileTypeId,
            Id = file.Id,
            URL = file.URL
        };
        return result;
    }
}
