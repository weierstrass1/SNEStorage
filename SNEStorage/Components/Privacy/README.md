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
and numbers its sections. StaticDocumentPage handles local JSON reading before rendering, retries,
anchor scrolling, and cleanup. PrivacyDocument is frontend presentation data,
not a database model or an API DTO. This page does not implement backend data handling.

## Content basis and publication review

The text reflects registration and profile data, email visibility, age-based
sensitive-content access, account/session/antiforgery protection, moderation
reports, Discord linking, Google Fonts, browser account memory, the IndexedDB SPC
library, browser-only IPS/BPS patching, and its aggregate usage counter stored in
a server-side file without uploading ROMs or patches. EmailService sends confirmation and
password-reset messages through the configured MonsterASP SMTP service
(`mail2305.mailasp.net`) using `support@snestorage.dev`; Mailtrap is no longer
listed as a current provider.

The owner confirmed USA-based MonsterASP Premium hosting and Cloudflare proxy,
DDoS protection, rate limiting, Bot Fight Mode, and Turnstile. These dashboard
settings are owner-supplied; application rate limits and Turnstile validation
were also checked in code. Provider documentation supports the descriptions of
technical signals, security cookies, and hosting protections. The text does not
promise immunity from attacks or invent enabled optional hosting add-ons.

The privacy contact opens the official SNEStorage Discord invitation
`https://discord.gg/e8bHBcRKX9`, rather than SNES Office. No personal address or
operator country was added.

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
applicable law. Provider logs and backup retention are described separately from
live-site deletion without promising a particular deletion deadline. Recheck
this notice when hosting, security settings, email, or local tools change.

References used for drafting (provider information reviewed 2026-10-16):

- ICO privacy-notice structure: https://ico.org.uk/for-organisations/uk-gdpr-guidance-and-resources/individual-rights/the-right-to-be-informed/what-privacy-information-should-we-provide/
- Turnstile: https://www.cloudflare.com/turnstile-privacy-policy/
- Cloudflare: https://www.cloudflare.com/privacypolicy/
- Cloudflare cookies: https://developers.cloudflare.com/fundamentals/reference/policies-compliances/cloudflare-cookies/
- Bot Fight Mode: https://developers.cloudflare.com/bots/get-started/bot-fight-mode/
- MonsterASP privacy and infrastructure security: https://www.monsterasp.net/Privacy/
- MonsterASP Premium plans: https://www.monsterasp.net/Pricing/
- Google: https://policies.google.com/privacy
- Discord: https://discord.com/privacy
