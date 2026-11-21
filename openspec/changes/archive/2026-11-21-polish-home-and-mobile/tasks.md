# Tasks — polish-home-and-mobile

- [x] 1. Failing `HomeTests`. **Record red run.**
- [x] 2. Home page per proposal; resources el/en.
- [x] 3. Mobile top bar `<details>` menu; fix any overflow found.
- [x] 4. README.md.
- [x] 5. Green: full `make test`. Record output.
- [x] 6. Playwright 390px overflow check + screenshots of all MVP pages (desktop + mobile), read and fixed.
- [x] 7. Clara audit; fix findings or record why not.
- [x] 8. Validate, commit, push, archive.

## Red run (task 1) — 2026-11-21

```
Failed HomeTests.Home_has_demo_cta_and_working_sample_profile_link
Failed HomeTests.Home_shows_live_demo_counts
Failed HomeTests.Home_in_english
Failed!  - Failed: 3, Passed: 42, Skipped: 0, Total: 45
```

## Green run (task 5) — 2026-11-21

```
dotnet build → 0 Warning(s), 0 Error(s)
make test    → Passed!  - Failed: 0, Passed: 45, Skipped: 0, Total: 45
```

## Mobile check (task 6) — Playwright + Brave headless, 390×844, scrollWidth / innerWidth
```
/                    390 / 390   (anonymous)
/p/{slug}            390 / 390   (anonymous and as coach)
/demo                390 / 390
/denied              390 / 390
/search              390 / 390   (coach)
/shortlist           390 / 390   (coach)
/me/inbox            390 / 390   (athlete)
```
First run of my check script signed in from /denied (no persona form there) so /search and /shortlist were measured
on the /demo page — caught by reading the screenshot, script fixed, re-run above.
Screenshots read: home desktop el/en, search mobile (details menu), inbox mobile. Kept in docs/screenshots/.

## Review fixes (orchestrator)
- Orphan resource `Home_Tagline` (unused after the new home page) removed from both .resx.
- README step 4 now names the Greek UI label «Σέντερ».
- Showcase athletes on /demo prefer athletes with interests waiting → inbox is not empty in the demo.
- `.playwright-cli/` (screenshot tool snapshots) added to .gitignore.

## Clara audit (task 7) — 2026-11-21
Verdict PASS WITH FIXES (1 critical, 6 major, 11 minor). All in-scope findings fixed in change `fix-audit-findings`
(archived right after this one); out-of-scope items recorded there.
