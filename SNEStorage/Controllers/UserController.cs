using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SNEStorage.Models;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

public sealed class UserController(SnestorageContext context, FileService files) : Controller
{
    [HttpGet("/User/Avatar/{userId}")]
    public async Task<IActionResult> Avatar(string userId, CancellationToken cancellationToken)
    {
        var userInfo = await context.UserInfos
            .Include(info => info.Avatar)
                .ThenInclude(file => file!.FileType)
            .FirstOrDefaultAsync(info => info.UserId == userId, cancellationToken);
        var avatar = userInfo?.Avatar;

        if (avatar is null || string.IsNullOrWhiteSpace(avatar.URL))
            return Redirect("/images/logo-box.png");

        string physicalPath;
        try
        {
            physicalPath = files.ResolveStoragePath(avatar.URL);
        }
        catch (InvalidOperationException)
        {
            return Redirect("/images/logo-box.png");
        }

        return System.IO.File.Exists(physicalPath)
            ? PhysicalFile(physicalPath, avatar.FileType?.Value ?? "image/png", enableRangeProcessing: true)
            : Redirect("/images/logo-box.png");
    }
}
