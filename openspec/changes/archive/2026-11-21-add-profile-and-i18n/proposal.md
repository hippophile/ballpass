# add-profile-and-i18n (MVP items M1 athlete profile, M6 Greek/English base)

## Driver
Founder: "build the mvp with all the fake user and data we discussed, dont stop until done" (2026-11-21).
Layout approved in chat (NCSA-style profile card, stats with source label, club history, video placeholder, share link).

## Why now
The profile is the first screen investors see and the shareable unit of the athlete-first strategy
(analysis §2 gap 6: "shareable /p/name profile with a WhatsApp preview card"). Doing i18n first means
every later page is built bilingual from the start instead of being retrofitted.

## Measured blast radius (2026-11-21)
| Item | Count |
|---|---|
| Razor pages | 3 (Home, NotFound, Error) |
| Layout components | 2 (MainLayout, NavMenu sidebar from template) |
| `App.razor` `<html lang>` | hardcoded `en` |
| Localization setup | none |

## Decision
1. Localization: `AddLocalization`, supported cultures `el` (default) and `en`; cookie culture provider.
   `.resx` resources `Resources/SharedResource.resx` (Greek, neutral) + `SharedResource.en.resx`.
   `<html lang>` follows the current UI culture.
2. `GET /culture/set?culture=xx&redirectUri=/path` sets the culture cookie and redirects. Only `el`/`en` accepted;
   only local redirect URIs (open-redirect guard) — anything else redirects to `/`.
3. Replace the template sidebar with a top bar: brand "Ballpass", language toggle (EL | EN). Mobile-friendly (wraps).
4. `GET /p/{slug}` athlete profile (public, read-only):
   - Initials avatar (no photos), full name (Greek in `el`, Latin in `en`), DEMO badge, "Looking for team" badge.
   - Position (localized long name + abbreviation), height cm, age, city, current club or "no club".
   - Stats table per season: league, games, minutes, points, rebounds, assists, steals, blocks, FG%, 3P%, FT%,
     and a source badge: self-reported / coach-confirmed / league-verified (mock).
   - Club history, bio, video placeholder ("no video added yet"), share URL.
   - Open Graph meta (`og:title`, `og:description`, `og:type=profile`) for WhatsApp/social previews.
   - Unknown slug → 404 page (with banner).
5. Region names localized (stored English keys → Greek/English labels).
6. Tests use a dedicated factory on DB `ballpass_test` seeded with seed 42; DB test classes share one xUnit
   collection so they never run in parallel against the same database.

## Verification
- Red first (profile and culture endpoints do not exist → 404s).
- Profile shows the right athlete data (from generator seed 42), both languages, source labels, og:title.
- Negative: unknown slug → 404; culture redirect to an external URL → redirected to `/`.
- Screenshots desktop + mobile, Greek and English.

## Caveats
- Age is computed from birth year (approximate by up to 1 year) — fine for a demo, stated.
- Position names in Greek use the common transliterated basketball terms (e.g. "Πόιντ γκαρντ").
- No auth yet: the profile is public by design (athlete-first sharing). Contact/shortlist buttons come in M3/M4.
