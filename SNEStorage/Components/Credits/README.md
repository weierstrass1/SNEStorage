# Credits

Public page `/Credits`, in the sidebar's collapsible Info group after About.
Edit `wwwroot/Content/credits.json` to add contributors or change descriptions.
Sections and entries keep the order written in JSON. Links accept HTTPS only,
using the existing AboutLink validation; text is rendered as escaped Razor text.

The creator uses PublicProfileLoader for the current public username color and
avatar, with a linked name fallback if the API is unavailable. The other entries
are static and require no account, new backend action, or database changes.
Leonardo Pinto is credited as the SNEStorage logo artist, with his portfolio link
and a local copy of the portrait published on his About page. Entries can set
optional `image` (a local `/images/` path) and `imageAlt` fields in the JSON.
The final Special thanks section acknowledges community testers and feedback.

Flips is credited as a reference: the browser patcher is an independent
implementation, not a port or execution of flips.exe. The SPC backend and BRR
decoder adapt the included Telinc1/snes_spc source and keep their notices.
Source, license, and build instructions link to the existing local distribution.
