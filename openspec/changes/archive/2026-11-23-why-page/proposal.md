# why-page (serves M0 skeleton, M1 athlete profile, M3 express interest, M5 demo login, M6 Greek/English)

## Driver
Founder (2026-11-23): "i want to showcase what we are solving here"; "the thing we doing its simple connecting athletes to coaches".
Audience: investors and people who might help, looking at the live app. Greek first (el default), English second.

## Measured blast radius (2026-11-23, branch from `wow-release` @ f558b71)
| Item | Count | Command |
|---|---|---|
| Existing routes at `/why` | 0 | `grep -rn '"/why"' src --include=*.razor` |
| Shared files touched | 3 (Home.razor 2 lines, MainLayout.razor 2 lines, App.razor 1 line) | `git diff --stat` |
| Existing tests | 446 `[Fact]`/`[Theory]` attributes | `grep -rhoE "\[(Fact\|Theory)" tests \| wc -l` |
| New resource pair / css file | 1 / 1 | `WhyResource(.en).resx`, `bp-why.css` |

## Decision
1. New static-SSR page `/why` ("Γιατί Ballpass"), a six-part scroll story: hook (full-bleed photo + scrim, one line),
   "Σήμερα" (a cluster of HTML/CSS fragments: forwarded message, phone call, league PDF, video link, group post, between the
   athlete "Δεν ξέρω ποιος ψάχνει" and the coach "Δεν ξέρω ποια υπάρχει"), the cost (3 consequences; any figure carries a
   footnote with a source link), "Με το Ballpass" (4 steps teal → orange → teal → connected, each with a mini preview of the real
   app UI filled from the featured personas), live counters from the database (same queries as Home), and two persona doors.
2. Home: a compact "Το πρόβλημα" band right after the doors (Σήμερα ✕ ×3 vs Με το Ballpass ✓ ×3) with a link to `/why`.
3. "Γιατί Ballpass" link in the footer and in the signed-out nav only (role navs untouched: parallel Messages work).
4. Own `WhyResource` pair, own `bp-why.css`, own components under `Components/Why/`. No Program.cs, no entities, no migration.
5. Research with sources: `docs/research/problem.md`. Notion "Project B" problem statement used for wording only; its
   validation/traction numbers are fake and never used.

## Out of scope
Features/Loop, Inbox, Sent, AthleteProfile, Messages, role navs. No new data, no new seed rows.
