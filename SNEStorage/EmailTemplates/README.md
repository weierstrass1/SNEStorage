# Confirmation email

These files contain presentation only. No backend code or configuration was changed.

- `Confirmation.html`: the styled email, with English copy, a purple card and yellow confirmation button.
- `Confirmation.txt`: the plain-text alternative, sent in the same MIME message.
- The subject is `Confirm your email | SNEStorage`.

Edit the copy directly in both templates. Main layout and colors use inline CSS; the optional media query reduces padding on small screens. This is an email document, so it does not use Blazor, Bootstrap, JavaScript or external web fonts. Email applications may change colors or omit decorative shadows. Check an actual delivered message in your target email applications before release.

## Template values

| Placeholder | Value |
| --- | --- |
| `{{Username}}` | Recipient's username |
| `{{ConfirmationUrl}}` | Absolute URL of the Blazor confirmation page, including user ID and encoded token |
| `{{SiteUrl}}` | Configured public website root URL |
| `{{Year}}` | Current UTC year |

HTML-encode every replacement in the HTML template, including URLs in `href`. Use the original values in the text template. URL encoding and HTML encoding are different steps: URL-encode when constructing the link, then HTML-encode when inserting that complete link in the HTML document.

The logo references `cid:snestorage-logo`. Attach `assets/caja.png` as a linked resource with `ContentId = "snestorage-logo"`. This is a copy of the project's existing optimized box logo (about 101 KB). It is part of the email, so the email does not need to retrieve it from localhost. The SNEStorage brand remains readable if images are hidden.

## Responsibilities and flow

1. `AccountService` completes registration and saves the user and profile.
2. `AccountService` generates an Identity email confirmation token for that user and creates the public confirmation URL.
3. `AccountService` calls `EmailService.Confirmation(email, username, confirmationUrl, ct)`.
4. `EmailService` fills these templates and sends the message using SMTP.
5. The recipient opens the Blazor page. The page calls the existing anonymous POST action to confirm the email.

Proposed method signature:

```csharp
public Task Confirmation(
    string email,
    string username,
    string confirmationUrl,
    CancellationToken ct = default);
```

`EmailService` does not query Identity or the database. An SMTP failure should be handled separately from account creation: the account may already be saved. Plan a resend action rather than asking the user to register the same account again. Require confirmed email in the login flow separately.

## Backend example for you to implement

This is documentation, not an installed service. Add MailKit to the web project yourself:

```powershell
dotnet add .\SNEStorage\SNEStorage.csproj package MailKit
```

Example configuration (add this to your configuration yourself):

```json
{
  "App": {
    "PublicUrl": "https://YOUR-PUBLIC-DOMAIN/"
  },
  "Email": {
    "Host": "smtp.gmail.com",
    "Port": 587,
    "SenderAddress": "pdiaz291191@gmail.com",
    "SenderName": "SNEStorage",
    "Username": "pdiaz291191@gmail.com"
  }
}
```

Set `Email:Password` to a Google app password using .NET User Secrets locally and a secret store or environment variable (`Email__Password`) in deployment. Do not commit the password or use your regular Google password. App passwords require two-step verification and may not be available for every Google account.

`App:PublicUrl` must be your configured website URL, not a value supplied by a request. A localhost URL is usable only on the computer running that local website; external recipients need the published domain.

### Token and URL generation, in AccountService

Run this after registration has successfully saved the account and profile. The token is an Identity confirmation token, not the login JWT.

```csharp
using System.Text;
using Microsoft.AspNetCore.WebUtilities;

var rawToken = await UserManager.GenerateEmailConfirmationTokenAsync(user);
var token = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(rawToken));
var publicUrl = Configuration["App:PublicUrl"]
    ?? throw new InvalidOperationException("App:PublicUrl is required.");
var siteUri = new Uri(publicUrl.TrimEnd('/') + "/", UriKind.Absolute);
var pageUri = new Uri(siteUri,
    $"Register/Confirmation/{Uri.EscapeDataString(user.Id)}");
var confirmationUrl = QueryHelpers.AddQueryString(pageUri.AbsoluteUri, "token", token);

await emailService.Confirmation(user.Email!, user.UserName!, confirmationUrl, ct);
```

The email link opens:

```text
GET https://YOUR-PUBLIC-DOMAIN/Register/Confirmation/{userId}?token={Base64UrlToken}
```

The existing frontend then sends:

```text
POST /api/register/confirmed/{userId}?token={Base64UrlToken}
```

Keep the encoded token intact in the email. Your `AccountService.ConfirmUser` already decodes it before calling `ConfirmEmailAsync`. The token provider determines expiry; this template deliberately does not promise a fixed number of hours.

### Registration success redirect

The frontend page `/Register/CheckEmail` tells the user to open their confirmation email. It redirects to Home when the Blazor authentication state identifies an authenticated user.

Two backend connections must be updated by the backend owner:

- `EmailService.Confirmation`: build the link with `?token=` instead of `?code=`.
- `RegisterController.Register`: on success, return `Redirect("/Register/CheckEmail")` instead of redirecting to `/Login?registered=1`.

The confirmation controller already accepts `[FromQuery] string token`; its route does not need to change.

### Example EmailService

This sample uses the application's configuration and environment. Register it yourself with `services.AddScoped<EmailService>()`, then inject it into `AccountService`.

```csharp
using System.Globalization;
using System.Text.Encodings.Web;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace SNEStorage.Services;

public class EmailService(
    IWebHostEnvironment environment,
    IConfiguration configuration)
{
    public async Task Confirmation(
        string email,
        string username,
        string confirmationUrl,
        CancellationToken ct = default)
    {
        if (!Uri.TryCreate(confirmationUrl, UriKind.Absolute, out var link)
            || (link.Scheme != Uri.UriSchemeHttps
                && !(environment.IsDevelopment()
                    && link.Scheme == Uri.UriSchemeHttp && link.IsLoopback)))
        {
            throw new ArgumentException("An absolute HTTPS confirmation URL is required.",
                nameof(confirmationUrl));
        }

        string Required(string key) => configuration[key]
            ?? throw new InvalidOperationException($"Missing configuration: {key}");

        var values = new Dictionary<string, string>
        {
            ["Username"] = username,
            ["ConfirmationUrl"] = confirmationUrl,
            ["SiteUrl"] = Required("App:PublicUrl"),
            ["Year"] = DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture)
        };

        // One pass: a username containing placeholder-like text stays plain user data.
        string Fill(string template, bool html) =>
            System.Text.RegularExpressions.Regex.Replace(template,
                @"\{\{(Username|ConfirmationUrl|SiteUrl|Year)\}\}", match =>
                {
                    var value = values[match.Groups[1].Value];
                    return html ? HtmlEncoder.Default.Encode(value) : value;
                });

        var folder = Path.Combine(environment.ContentRootPath, "EmailTemplates");
        var htmlTemplate = await System.IO.File.ReadAllTextAsync(
            Path.Combine(folder, "Confirmation.html"), ct);
        var textTemplate = await System.IO.File.ReadAllTextAsync(
            Path.Combine(folder, "Confirmation.txt"), ct);
        var body = new BodyBuilder
        {
            HtmlBody = Fill(htmlTemplate, html: true),
            TextBody = Fill(textTemplate, html: false)
        };

        var logoPath = Path.Combine(folder, "assets", "caja.png");
        var logo = body.LinkedResources.Add(logoPath);
        logo.ContentId = "snestorage-logo";

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(
            configuration["Email:SenderName"] ?? "SNEStorage",
            Required("Email:SenderAddress")));
        message.To.Add(new MailboxAddress(username, email));
        message.Subject = "Confirm your email | SNEStorage";
        message.Body = body.ToMessageBody();

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(Required("Email:Host"),
            configuration.GetValue<int?>("Email:Port") ?? 587,
            SecureSocketOptions.StartTls, ct);
        await smtp.AuthenticateAsync(Required("Email:Username"),
            Required("Email:Password"), ct);
        await smtp.SendAsync(message, ct);
        await smtp.DisconnectAsync(true, ct);
    }
}
```

### Include templates when publishing

These files are outside `wwwroot`. Add the following to the web `.csproj` yourself so the templates and logo ship with the application:

```xml
<ItemGroup>
  <None Update="EmailTemplates\Confirmation.html;EmailTemplates\Confirmation.txt;EmailTemplates\assets\caja.png"
        CopyToOutputDirectory="PreserveNewest"
        CopyToPublishDirectory="PreserveNewest" />
</ItemGroup>
```

No package, configuration, service registration, controller or service was changed as part of creating these templates. No real email was sent.

## References

- [ASP.NET Core account confirmation](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/accconfirm?view=aspnetcore-10.0)
- [MailKit](https://github.com/jstedfast/MailKit)
- [MimeKit linked resources](https://mimekit.net/docs/html/P_MimeKit_BodyBuilder_LinkedResources.htm)
- [Gmail SMTP settings](https://support.google.com/mail/answer/7104828?hl=en)
- [Google app passwords](https://support.google.com/accounts/answer/185833?hl=es)
