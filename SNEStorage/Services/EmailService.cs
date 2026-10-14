using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using SNEStorage.DTOs;
using System.Net;

namespace SNEStorage.Services;

public class EmailService(IWebHostEnvironment webHostEnvironment,
    DateTimeService dateTimeService, APIConfig apiConfig,
    DBHelperService dbHelperService)
{
    private readonly IWebHostEnvironment _webHostEnvironment = webHostEnvironment;
    private readonly DateTimeService _dateTimeService = dateTimeService;
    private readonly APIConfig _apiConfig = apiConfig;
    private readonly DBHelperService _dbHelperService = dbHelperService;

    public async Task<ServiceResultContext> Confirmation(
        string username, string userId, string email, string token,
        CancellationToken ct = default)
    {
        string path = Path.Combine(_webHostEnvironment.ContentRootPath,
            "EmailTemplates", "Confirmation.html");

        string url = $"https://snestorage.dev/Register/Confirmation/" +
            $"{Uri.EscapeDataString(userId)}?token={Uri.EscapeDataString(token)}";

        var yearResult = await _dateTimeService.GetYear(userId, ct);

        if (!yearResult.TryPickT0(out var year, out var yearError))
            return yearError;

        var htmlResult = await _dbHelperService.TryFunction(
            cancel => File.ReadAllTextAsync(path, cancel),
            ServiceResult.TemplateLoadError, ct: ct);

        if (!htmlResult.TryPickT0(out var html, out var templateError))
            return templateError;

        html = html
            .Replace("{{ConfirmationUrl}}", WebUtility.HtmlEncode(url))
            .Replace("{{Username}}", WebUtility.HtmlEncode(username))
            .Replace("{{Year}}", year.ToString())
            .Replace("{{SiteUrl}}", WebUtility.HtmlEncode("https://snestorage.dev/"));

        var body = new BodyBuilder
        {
            HtmlBody = html,
            TextBody = $"Hi {username},\n\nConfirm your email:\n{url}\n\n" +
                "If you didn't create this account, ignore this email."
        };

        return await SendEmail("Confirm your email | SNEStorage",
            body, new MailboxAddress(username, email), ct);
    }

    public async Task<ServiceResultContext> PasswordReset(
        string username, string userId, string email, string token,
        CancellationToken ct = default)
    {
        string path = Path.Combine(_webHostEnvironment.ContentRootPath,
            "EmailTemplates", "PasswordReset.html");

        var yearResult = await _dateTimeService.GetYear(userId, ct);

        if (!yearResult.TryPickT0(out var year, out var yearError))
            return yearError;

        const string siteUrl = "https://snestorage.dev/";

        string url = $"{siteUrl}Account/ResetPassword/" +
            $"{Uri.EscapeDataString(userId)}?token={Uri.EscapeDataString(token)}";

        var htmlResult = await _dbHelperService.TryFunction(
            cancel => File.ReadAllTextAsync(path, cancel),
            ServiceResult.TemplateLoadError, ct: ct);

        if (!htmlResult.TryPickT0(out var html, out var templateError))
            return templateError;

        html = html
            .Replace("{{PasswordResetUrl}}", WebUtility.HtmlEncode(url))
            .Replace("{{Username}}", WebUtility.HtmlEncode(username))
            .Replace("{{Year}}", year.ToString())
            .Replace("{{SiteUrl}}", WebUtility.HtmlEncode(siteUrl));

        var body = new BodyBuilder
        {
            HtmlBody = html,
            TextBody = $"Hi {username},\n\nPassword Change:\n{url}\n\n" +
                "If you didn't request a password change, ignore this email."
        };

        return await SendEmail("Password Change | SNEStorage",
            body, new MailboxAddress(username, email), ct);
    }

    private async Task<ServiceResultContext> SendEmail(
        string subject, BodyBuilder body, MailboxAddress recipient,
        CancellationToken ct = default)
    {
        string logoPath = Path.Combine(_webHostEnvironment.ContentRootPath,
            "EmailTemplates", "assets", "caja.png");

        var logoResult = _dbHelperService.TryFunction(
            () => body.LinkedResources.Add(logoPath, ct),
            ServiceResult.TemplateLoadError);

        if (!logoResult.TryPickT0(out var logo, out var logoError))
            return logoError;

        logo.ContentId = "snestorage-logo";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("SNEStorage", _apiConfig.Email));
        message.To.Add(recipient);
        message.Subject = subject;
        message.Body = body.ToMessageBody();

        using var smtp = new SmtpClient();

        var connectionResult = await _dbHelperService.TryFunction(
            cancel => smtp.ConnectAsync(_apiConfig.EmailSMPTP, _apiConfig.EmailPort,
                SecureSocketOptions.StartTls, cancel),
            ServiceResult.EmailConnectionError, ct: ct);

        if (connectionResult.Result != ServiceResult.Success)
            return connectionResult;

        var authenticationResult = await _dbHelperService.TryFunction(
            cancel => smtp.AuthenticateAsync(
                _apiConfig.Email, _apiConfig.EmailPassword, cancel),
            ServiceResult.EmailConnectionError, ct: ct);

        if (authenticationResult.Result != ServiceResult.Success)
            return authenticationResult;

        var sendResult = await _dbHelperService.TryFunction(
            cancel => smtp.SendAsync(message, cancel),
            ServiceResult.EmailConnectionError, ct: ct);

        if (!sendResult.TryPickT0(out _, out var sendError))
            return sendError;

        return await _dbHelperService.TryFunction(
            cancel => smtp.DisconnectAsync(true, cancel),
            ServiceResult.EmailConnectionError, ct: ct);
    }
}
