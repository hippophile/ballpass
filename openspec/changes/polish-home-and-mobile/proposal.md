# polish-home-and-mobile (MVP polish; M6 completion)

## Driver
Founder: "build the mvp ... dont stop until done" (2026-11-21). Last build session (analysis §8: "i18n + polish +
demo recording").

## Why now
All MVP items work, but the home page is a placeholder and the mobile top bar stacks into ~180px before content.
Investors open the home page first, often on a phone.

## Measured blast radius (2026-11-21)
| Item | Count |
|---|---|
| Home page | 2 lines (heading + tagline) |
| Mobile top bar height at 390px | ~185px (screenshot of /search) |
| README | none |

## Decision
1. Home page `/` (public, static SSR):
   - Hero: brand, one-line value proposition, sub-line "women's basketball first, Greece".
   - Two call-to-action links: "Try as a coach" → `/demo` and "See a sample athlete profile" → a showcase athlete
     (woman, with club, most seasons) chosen from the DB.
   - "How it works" in 3 steps (athlete creates profile → coach searches → coach expresses interest).
   - Trust strip: the three stat-source labels explained (self-reported / coach-confirmed / league-verified mock).
   - Live counters from the DB (athletes, clubs, coaches) labelled as demo data.
   - Markup contracts: CTA `<a ... data-cta="demo" href="/demo">`, `<a ... data-cta="sample" href="/p/{slug}">`.
2. Mobile top bar: on ≤ 640px the links collapse into a `<details>` menu ("Μενού"/"Menu"), no JavaScript;
   brand + language toggle stay visible.
3. `README.md`: what Ballpass is, how to run (`make setup`, `make run`, `make test`, `make reset`), demo walkthrough
   script (3 minutes, for investors), what is fictional, known limits, MVP item map to openspec specs.
4. Final independent audit by Clara (read-only) over the repo against openspec specs + config rules.

## Verification
- Red first: `HomeTests` (CTAs, sample profile link resolves to 200, counters, both languages).
- Mobile: Playwright at 390px → `document.documentElement.scrollWidth <= innerWidth` on `/`, `/search`,
  `/p/{slug}`, `/demo`, `/shortlist`, `/me/inbox`; screenshots of each read by the orchestrator.

## Caveats
- No demo video recording in this change (screenshots instead); recording can be made by hand from the README script.
- Deployment to the VM is out of scope (founder: "local for now").
