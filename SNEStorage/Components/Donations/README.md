# Donations page

The public `/Donations` page loads `wwwroot/Content/donations.json` through the existing `StaticDocumentPage` browser loader. No account or backend endpoint is needed.

Edit the JSON to update the titles, introductory text, Patreon URL, supporter benefits, support cards, and thank-you message. `benefitsTitle` sets the supporter section heading; `benefits` contains editable `{ title, text }` cards. An empty benefits list hides that section. Keep at least one item in `sections`. The Patreon button accepts an absolute HTTPS URL on `patreon.com` or `www.patreon.com`; invalid URLs show the configured unavailable message. Text is rendered as plain text.

The initial link is https://www.patreon.com/anonimzwx. The page describes supporter channel access, voting on selected projects and site improvements, and external emoji permissions. These are informational cards; Discord roles, permissions and voting are managed separately. Membership options and prices stay on Patreon.

The view, code-behind, and isolated styles are separated in `Pages/Donations.razor`, `Pages/Donations.razor.cs`, and `Pages/Donations.razor.css`.
