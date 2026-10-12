using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using System.Net;

namespace SNEStorage.Services;
public enum EmailServiceResult
{
    Success,
    TemplateLoadError,
    EmailConnectionError,
    UserNotFound
}
public class EmailService(IWebHostEnvironment webHostEnvironment,
    DateTimeService dateTimeService, 
    APIConfig apiConfig,
    DBHelperService dbHelperService)
{
    private readonly IWebHostEnvironment _webHostEnvironment = webHostEnvironment;
    private readonly DateTimeService _dateTimeService = dateTimeService;
    private readonly APIConfig _apiConfig = apiConfig;
    private readonly DBHelperService _dbHelperService = dbHelperService;

    public async Task<EmailServiceResult> Confirmation(string username, string userId, string email, string token, CancellationToken ct = default)
    {
        var path = Path.Combine(_webHostEnvironment.ContentRootPath, "EmailTemplates", 
            "Confirmation.html");

        var url = $"https://snestorage.dev/Register/Confirmation/" +
            $"{Uri.EscapeDataString(userId)}?token={Uri.EscapeDataString(token)}";

        int? year = await _dbHelperService.TryFunction(
            async cancel => await _dateTimeService.GetYear(userId, cancel), ct: ct);
        if (year == null)
            return EmailServiceResult.UserNotFound;

        var html = await _dbHelperService.TryFunction(
            async cancel => await File.ReadAllTextAsync(path, cancel), ct: ct);
        if (html == null)
            return EmailServiceResult.TemplateLoadError;

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
            body, new(username, email), ct);
    }
    public async Task<EmailServiceResult> PasswordReset(string username, string userId,
        string email, string token, CancellationToken ct = default)
    {
        var path = Path.Combine(_webHostEnvironment.ContentRootPath, "EmailTemplates",
            "PasswordReset.html");

        int? year = await _dbHelperService.TryFunction(
            async cancel => await _dateTimeService.GetYear(userId, cancel), ct: ct);
        if (year == null)
            return EmailServiceResult.UserNotFound;

        var siteUrl = "https://snestorage.dev/";
        var url = $"{siteUrl}Account/ResetPassword/" +
            $"{Uri.EscapeDataString(userId)}?token={Uri.EscapeDataString(token)}";

        var html = await _dbHelperService.TryFunction(
            async cancel => await File.ReadAllTextAsync(path, cancel), ct: ct);
        if (html == null)
            return EmailServiceResult.TemplateLoadError;

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
            body, new(username, email), ct);
    }
    private async Task<EmailServiceResult> SendEmail(string subject, BodyBuilder body, MailboxAddress To, CancellationToken ct = default)
    {
        var logoPath = Path.Combine(_webHostEnvironment.ContentRootPath,
            "EmailTemplates", "assets", "caja.png");

        var logo = _dbHelperService.TryFunction(
            () => body.LinkedResources.Add(logoPath, ct));

        if (logo == null)
            return EmailServiceResult.TemplateLoadError;

        logo.ContentId = "snestorage-logo";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("SNEStorage", _apiConfig.Email));
        message.To.Add(To);
        message.Subject = subject;
        message.Body = body.ToMessageBody();
        using var smtp = new SmtpClient();
        return await _dbHelperService.TryFunction(
            async cancel =>
            {
                await smtp.ConnectAsync(_apiConfig.EmailSMPTP, _apiConfig.EmailPort,
                    SecureSocketOptions.StartTls, ct);

                await smtp.AuthenticateAsync(_apiConfig.Email, _apiConfig.EmailPassword, ct);
                await smtp.SendAsync(message, ct);
                await smtp.DisconnectAsync(true, ct);
            }, ct: ct) ?
            EmailServiceResult.Success :
            EmailServiceResult.EmailConnectionError;
    }
}
