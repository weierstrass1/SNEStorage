# Privacy page

Public routes: `/Privacy` and the retained `/Home/Privacy` alias. Navigation and
footer use `/Privacy`. There is no account gate or new controller action.

Edit `wwwroot/Content/privacy-policy.json` to change every policy heading,
paragraph, contact destination, provider link, and revision date. Keep section
and entry IDs unique and stable for direct links. Sections receive letters and
entries receive per-section numbers through the existing document helper.
The contact URL accepts HTTPS or `mailto:`; invalid schemes are not rendered as
links. Use `mailto:your-public-address@example.com` to replace Discord with email.

Privacy.razor contains markup; Privacy.razor.cs only selects the public JSON file
and numbers its sections. StaticDocumentPage handles browser loading, retries,
anchor scrolling, and cleanup. PrivacyDocument is frontend presentation data,
not a database model or an API DTO. No backend files were modified.

## Content basis and publication review

The initial text reflects RegisterInfo, UserInfo, UserNameDTO's email visibility,
AccountService, session/antiforgery configuration, the browser account snapshot,
resource permissions, externally loaded fonts, and the current Mailtrap SMTP
configuration. US server location was supplied by the site owner. The contact
link is the community Discord already used by the footer; no personal email,
home address, or operator country was added.

This is an editable starting notice, not a declaration of legal compliance.
Before public release, the operator should confirm the applicable jurisdiction,
required controller identity/contact details, purposes for each mandatory field
(particularly date of birth), applicable legal bases, international transfer
arrangements, retention/deletion criteria (including hosting backups and logs),
and handling of minors where relevant. No minimum age, deletion deadline, backup
schedule, or guaranteed deletion feature was invented. The page adds no backend
retention, consent, account export, or deletion implementation.

Current accounts/uploads do not have automatic expiry. The text states this
rather than promising an unimplemented cleanup schedule. Staff handling of data
requests is distinct from resource-deletion policies and must be reviewed for the
applicable law. Update the email provider before launch if Mailtrap's development
sandbox is replaced. Recheck the Discord invitation and contact availability.

References used for drafting (reviewed 2026-10-09):

- ICO privacy-notice structure: https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/individual-rights/the-right-to-be-informed/what-privacy-information-should-we-provide/
- Turnstile: https://www.cloudflare.com/turnstile-privacy-policy/
- Mailtrap: https://mailtrap.io/privacy/
- Google: https://policies.google.com/privacy
- Discord: https://discord.com/privacy
