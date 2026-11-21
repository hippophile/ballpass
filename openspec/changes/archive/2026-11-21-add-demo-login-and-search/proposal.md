# add-demo-login-and-search (MVP items M5 demo login, M2 coach search)

## Driver
Founder: "build the mvp with all the fake user and data we discussed, dont stop until done" (2026-11-21).

## Why now
Search is the coach's core job (MVP must-have). The analysis (§4) closes the "coach uses an athlete account to avoid
paying" loophole by **gating by role**: only coach accounts can search; athletes cannot search other athletes.
Interest (M3) and shortlist (M4) also need a signed-in coach, so login comes first.

## Measured blast radius (2026-11-21, before change 3 lands)
| Item | Count |
|---|---|
| Authentication setup | none |
| Pages needing auth | 0 today; after this change: /search (coach), /denied |
| Persona sources | 30 demo coaches, 300 demo athletes in seed-42 data |

## Decision
1. Cookie authentication (no ASP.NET Identity tables): this is a demo persona switch, not real accounts.
   Real accounts (Identity, email verification) are pilot work — out of POC scope, stated in README.
2. `GET /demo` lists personas: head coaches (with club) and a sample of athletes. Each is a POST form (antiforgery)
   to `/demo/login` with `personaType` (coach|athlete) and `personaId`, optional `returnUrl` (local only).
   Claims: NameIdentifier, Name, Role (Coach|Athlete), ClubId (coach), Slug (athlete).
   After login: returnUrl if local, else coach → `/search`, athlete → own profile.
3. `POST /demo/logout` → signed out → `/`.
4. Authorization policy `CoachOnly` (role Coach). `/search` requires it. Anonymous → 302 `/demo?ReturnUrl=...`.
   Signed-in athlete → 302 `/denied` which returns **403** with a localized "coaches only" message.
5. `/search` (GET form, shareable URL): gender (default women), position (multi), min/max height, min/max age,
   region, league (of current club), looking-for-team, name text (Greek or Latin, case-insensitive contains).
   20 results per page, `page` parameter, total count. Result card: initials, name, position, height, age, club,
   looking badge, link to `/p/{slug}`. Markup contract for tests: `<article class="result" data-slug="...">`,
   total in an element with `data-total="N"`.
6. Top bar: signed-in persona name + role + "logout"; "Search" link for coaches; "Demo login" when anonymous.
7. All strings in the existing resources (el/en).

## Verification
- Red first; tests sign in through the real `/demo` form (antiforgery token parsed from the page).
- Each filter's result total equals the count computed from the seed-42 generator with the same filter.
- Negative: anonymous → login redirect; athlete → 403; open redirect via returnUrl blocked; logout removes access.

## Caveats
- Name search is not accent-insensitive in Greek ("Στελλα" will not match "Στέλλα") — noted for later (`unaccent`).
- Anyone can pick any persona — acceptable for an investor demo; the banner states it is a demo.
