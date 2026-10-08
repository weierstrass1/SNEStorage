using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

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
            .Replace("{{ConfirmationUrl}}", url) 
            .Replace("{{Username}}", username)
            .Replace("{{Year}}", year.ToString())
            .Replace("{{SiteUrl}}", "https://snestorage.dev/");

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress("SNEStorage", _apiConfig.Email));
        message.To.Add(new MailboxAddress(username, email));
        message.Subject = "Confirm your email | SNEStorage";

        var body = new BodyBuilder
        {
            HtmlBody = html,
            TextBody = $"Hi {username},\n\nConfirm your email:\n{url}\n\n" +
               "If you didn't create this account, ignore this email."
        };

        var logoPath = Path.Combine(
            _webHostEnvironment.ContentRootPath,
            "EmailTemplates",
            "assets",
            "caja.png");

        var logo = body.LinkedResources.Add(logoPath);
        logo.ContentId = "snestorage-logo";

        message.Body = body.ToMessageBody();

        using var smtp = new SmtpClient();

        try
        {
            await smtp.ConnectAsync("sandbox.smtp.mailtrap.io", 2525, 
                SecureSocketOptions.StartTls, ct);

            await smtp.AuthenticateAsync(_apiConfig.Email, _apiConfig.EmailPassword, ct);
            await smtp.SendAsync(message, ct);
            await smtp.DisconnectAsync(true, ct);
            return EmailServiceResult.Success;
        }
        catch
        {
            return EmailServiceResult.EmailConnectionError;
        }
    }
}
