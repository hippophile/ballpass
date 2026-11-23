# english-default (serves M6 Greek/English, M0 skeleton)

## Driver
Founder (2026-11-23): "the english language in most of the things are better for this kinda app i think".
Lead decision: English is the default UI culture; Greek stays fully available through the EN | EL toggle (cookie).
User-generated content (posts, comments, messages, notes) stays as written (Greek): authentic for a Greek market, never translated.
Person names use the existing Latin-name fields in English; club names and cities show as stored where no Latin field exists.

## Measured blast radius (2026-11-23, branch from `wow-release` @ dc470f8)
| Item | Count | Command |
|---|---|---|
| Test attributes | 476 `[Fact]`/`[Theory]` (~650 cases) | `grep -rhoE "\[(Fact\|Theory)" tests \| wc -l` |
| English resource files / keys | 11 / 757 | `grep -c "<data " src/Ballpass.Web/Resources/*.en.resx` |
| Culture providers today | 1 (cookie only; no Accept-Language) | `Program.cs` |
| Shared files touched | Program.cs (culture block), appsettings.json (+1 section), MainLayout.razor (toggle order), Infrastructure.cs (+1 setting) | `git diff --stat` |

## Decision
1. `Localization:DefaultCulture` in appsettings.json = `en` (supported: `el`, `en`; anything else fails at startup).
   Program.cs reads it instead of the hardcoded `el`.
2. Culture providers: cookie only. `AcceptLanguageHeaderRequestCultureProvider` (and the query-string provider) stay off on purpose,
   so a first visit renders the configured default whatever the browser sends: the demo is predictable. The toggle cookie still wins.
3. Header toggle order: EN | EL.
4. Tests: the existing suite asserts Greek on default requests; the shared test host is pinned to Greek
   (`UseSetting("Localization:DefaultCulture","el")`) instead of rewriting ~650 cases. New tests use a separate host with the default
   config (own database) and prove English on the key pages with no cookie and with `Accept-Language: el`.
5. English copy pass over every `*.en.resx`: US English, sentence case, short, active voice (LinkedIn / Strava / Hudl tone).
   Meaning unchanged; "demo / sample / fictional" only in the footer disclosure and the /demo intro.

## Out of scope
Translating seeded user content, new Latin fields for clubs/cities (reported instead), Greek copy changes.
