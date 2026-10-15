using Microsoft.EntityFrameworkCore;
using OneOf;
using SNEStorage.DTOs;
using SNEStorage.DTOs.QueryOperations;
using SNEStorage.Models;
using SNEStorage.Services.QueryComponents;

namespace SNEStorage.Services;

public class UserService(SnestorageContext context,
    GetLikeBySelectorQueryOperation<UserInfo, UserNameDTO> getLike)
{
    public const int MAX_AMOUNT_LIMIT = 20;

    private readonly SnestorageContext _context = context;
    private readonly GetLikeBySelectorQueryOperation<UserInfo, UserNameDTO> _getLike = getLike;

    public Task<OneOf<List<UserNameDTO>, ServiceResultContext>> GetUserLike(
        string likeString, int maxAmount = 10, CancellationToken ct = default)
    {
        var query = _context.UserInfos.AsNoTracking();
        var parameter = new GetLikeBySelectorQuery<UserInfo, UserNameDTO>(
            likeString, maxAmount, MAX_AMOUNT_LIMIT,
            user => user.User!.UserName!,
            user => new UserNameDTO
            {
                Id = user.UserId,
                Username = user.User!.UserName!,
                UsernameColor = user.UsernameColor
            });

        return _getLike.Execute(query, parameter, ct: ct);
    }
}
