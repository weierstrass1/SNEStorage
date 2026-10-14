using OneOf;
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
        foreach ((T target, ApplicationUser source) in usersFunc())
            set(target, FromApplicationUser(source, accountService));
    }

    public static void FillUserSummaries<T>(Action<T, UserNameDTO> set,
        Func<IEnumerable<(T, UserInfo)>> usersFunc,
        AccountService? accountService = null)
    {
        foreach ((T target, UserInfo source) in usersFunc())
            set(target, FromUserInfo(source, accountService));
    }

    public static UserNameDTO FromApplicationUser(
        ApplicationUser user, AccountService? accountService = null)
    {
        if (user.UserInfo is not null)
            return FromUserInfo(user.UserInfo, accountService);

        return new UserNameDTO
        {
            Id = user.Id,
            Username = user.UserName!
        };
    }

    public static async Task<OneOf<UserNameDTO, ServiceResultContext>> FromUser(
        AccountService accountService)
    {
        var userResult = await accountService.GetUser();

        if (!userResult.TryPickT0(out var user, out var userError))
            return userError;

        return FromApplicationUser(user, accountService);
    }

    public static UserNameDTO FromUserInfo(
        UserInfo user, AccountService? accountService = null)
    {
        var result = new UserNameDTO
        {
            Id = user.UserId,
            Username = user.User!.UserName!,
            UsernameColor = user.UsernameColor,
            Email = user.EmailVisibilityId == 1 ||
                accountService?.GetUserID() == user.UserId ?
                    user.User.Email : null
        };

        if (user.Avatar is null)
            return result;

        result.Avatar = new FileResponse
        {
            FileTypeId = user.Avatar.FileTypeId,
            Id = user.Avatar.Id,
            URL = user.Avatar.URL
        };

        return result;
    }
}
