using SNEStorage.DTOs;

namespace SNEStorage.Services;

public class CaptchaService(HttpClient httpClient, APIConfig apiConfig,
    DBHelperService dbHelperService, ILogger<CaptchaService> logger)
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly APIConfig _apiConfig = apiConfig;
    private readonly DBHelperService _dbHelperService = dbHelperService;
    private readonly ILogger<CaptchaService> _logger = logger;

    public async Task<ServiceResultContext> ValidateAsync(
        string? token, string expectedAction, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 2048)
            return ServiceResult.Invalid.Return(_logger);

        string secret = _apiConfig.TurnstileSecretKey;
        string[] allowedHostnames = _apiConfig.TurnstileAllowedHostnames;

        if (string.IsNullOrWhiteSpace(secret) || allowedHostnames.Length == 0)
            return ServiceResult.Unavailable.Return(_logger);

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["secret"] = secret,
            ["response"] = token
        });

        var responseResult = await _dbHelperService.TryFunction(
            cancel => _httpClient.PostAsync(
                "https://challenges.cloudflare.com/turnstile/v0/siteverify",
                content, cancel),
            ServiceResult.Unavailable, ct: ct);

        if (!responseResult.TryPickT0(out var response, out var responseError))
            return responseError;

        using var responseScope = response;

        if (!response.IsSuccessStatusCode)
            return ServiceResult.Unavailable.Return(_logger);

        var verificationResult = await _dbHelperService.TryFunction(
            cancel => response.Content.ReadFromJsonAsync<TurnstileResponse>(
                cancellationToken: cancel),
            ServiceResult.Unavailable, ct: ct);

        if (!verificationResult.TryPickT0(
            out var verification, out var verificationError))
            return verificationError;

        if (verification is null)
            return ServiceResult.Unavailable.Return(_logger);

        bool valid = verification.Success &&
            string.Equals(verification.Action, expectedAction, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(verification.Hostname) &&
            allowedHostnames.Contains(
                verification.Hostname, StringComparer.OrdinalIgnoreCase);

        return valid ?
            ServiceResult.Valid.Return(_logger) :
            ServiceResult.Invalid.Return(_logger);
    }
}
