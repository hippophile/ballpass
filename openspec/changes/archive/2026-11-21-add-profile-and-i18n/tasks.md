# Tasks — add-profile-and-i18n (M1, M6 base)

- [x] 1. Test infra: `BallpassAppFactory` (DB `ballpass_test`, seeded once) + xUnit collection `db`; failing tests `ProfileTests`, `LocalizationTests`. **Record red run.**
- [x] 2. Localization setup, resources (el neutral, en), `<html lang>`, `/culture/set` with guards.
- [x] 3. Top bar layout replacing the template sidebar; language toggle.
- [x] 4. `/p/{slug}` profile page per spec, Open Graph meta, 404 for unknown slug, localized regions/positions/source labels.
- [x] 5. Green: full `make test`. Record output.
- [x] 6. Screenshots: profile desktop (el, en) + mobile (el).
- [x] 7. Validate, commit, push, archive.

## Red run (task 1) — 2026-11-21

```
Failed ProfileTests.Culture_switch_blocks_open_redirect
Failed ProfileTests.Athlete_without_club_shows_looking_for_team
Failed ProfileTests.Open_graph_title_contains_full_name
Failed ProfileTests.Coach_confirmed_label_in_greek
Failed ProfileTests.Culture_switch_sets_cookie_and_redirects_locally
Failed ProfileTests.English_cookie_switches_language_names_and_labels
Failed ProfileTests.Greek_is_the_default_language
Failed ProfileTests.Profile_shows_identity_club_stats_and_share_link
Failed!  - Failed: 8, Passed: 15, Skipped: 0, Total: 23
```
`Unknown_slug_is_404_with_banner` already passes (router 404 + layout banner) — kept as a regression guard.
Seeder test moved to its own DB `ballpass_seedtest` so dropping it cannot disturb the app factory's `ballpass_test`.

## Green run (task 5) — 2026-11-21

```
make test → Passed!  - Failed: 0, Passed: 23, Skipped: 0, Total: 23
```

## Screenshot review fixes (orchestrator, task 6)
Screenshots desktop el/en + mobile 390px were read and showed 4 defects, fixed:
- Bootstrap `.card` forced `flex-direction: column` → header avatar/name misaligned. Set row explicitly (column stays on ≤480px).
- Bootstrap `.placeholder` (loading skeleton) hid the "no video" text → renamed class `video-empty`.
- Greek source badges clipped in the stats table → shorter Greek column headers (Αγ., Λεπ., Πόν., Ριμπ., Ασ., Κλεψ., Μπλ.), badge wraps.
- Numbers shown as "38" next to "28,8" → always one decimal.

## Known limits (accepted for the demo)
- English view still shows Greek club names and Greek bios (demo data is Greek-only).
- Share URL uses the request host; behind a reverse proxy it needs forwarded headers (deploy work).
