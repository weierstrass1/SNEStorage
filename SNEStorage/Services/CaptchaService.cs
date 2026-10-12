using SNEStorage.DTOs;

namespace SNEStorage.Services;

public enum CaptchaResult
{
    Valid,
    Invalid,
    Unavailable
}

public class CaptchaService(
    HttpClient httpClient,
    APIConfig apiConfig,
    DBHelperService dbHelperService)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly APIConfig _apiConfig = apiConfig;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    public async Task<CaptchaResult> ValidateAsync(
        string? token,
        string expectedAction,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048)
            return CaptchaResult.Invalid;

        var secret = _apiConfig.TurnstileSecretKey;
        var allowedHostnames = _apiConfig.TurnstileAllowedHostnames;

        if (string.IsNullOrWhiteSpace(secret) || allowedHostnames.Length == 0)
            return CaptchaResult.Unavailable;

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["secret"] = secret,
                ["response"] = token
            });

        using var response = await _dbHelperService.TryFunction(
            async cancel => await _httpClient.PostAsync(
                "https://challenges.cloudflare.com/turnstile/v0/siteverify",
                content,
                cancel), ct: ct);
        if(response is null || !response.IsSuccessStatusCode)
            return CaptchaResult.Unavailable;

        var result = await _dbHelperService.TryFunction(
            response.Content
            .ReadFromJsonAsync<TurnstileResponse>, ct: ct);

        if (result is null)
            return CaptchaResult.Unavailable;

        bool valid = result.Success &&
                    string.Equals(result.Action, expectedAction, StringComparison.Ordinal) &&
                    allowedHostnames.Contains(result.Hostname, StringComparer.OrdinalIgnoreCase);

        return valid ? CaptchaResult.Valid : CaptchaResult.Invalid;
    }
}