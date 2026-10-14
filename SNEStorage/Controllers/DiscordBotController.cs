using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SNEStorage.DTOs;
using SNEStorage.Services;

namespace SNEStorage.Controllers;

[ApiController]
[Route("api/internal/discord")]
[IgnoreAntiforgeryToken]
public sealed class DiscordBotController(DiscordService discordService,
    IOptions<DiscordDTO> discordOptions) : ControllerBase
{
    private const string BotKeyHeader = "X-SNEStorage-Bot-Key";
    private readonly DiscordService _discordService = discordService;
    private readonly DiscordDTO _discordOptions = discordOptions.Value;

    [HttpGet("member-state/{discordUserId}")]
    [ProducesResponseType<DiscordMemberStateDTO>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DiscordMemberStateDTO>> GetMemberState(
        [FromRoute] string discordUserId, CancellationToken ct)
    {
        if (!HasValidBotKey())
            return Unauthorized($"{GetType().Name}-{ServiceResultTypes.Unauthorized}");

        var result = await _discordService.GetMemberStateAsync(discordUserId, ct);
        return this.GetActionResult(result);
    }
    private bool HasValidBotKey()
    {
        string suppliedKey = Request.Headers[BotKeyHeader].ToString();
        string expectedKey = _discordOptions.BotApiKey;

        if (string.IsNullOrWhiteSpace(suppliedKey) || string.IsNullOrWhiteSpace(expectedKey))
            return false;

        byte[] suppliedHash = SHA256.HashData(Encoding.UTF8.GetBytes(suppliedKey));

        byte[] expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expectedKey));

        return CryptographicOperations.FixedTimeEquals(suppliedHash, expectedHash);
    }
}
