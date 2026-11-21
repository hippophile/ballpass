# Tasks — add-demo-login-and-search (M5, M2)

- [x] 1. Failing tests `DemoLoginTests`, `SearchTests` (sign in via the real `/demo` form). **Record red run.**
- [x] 2. Cookie auth, `CoachOnly` policy, `/demo` persona page, `/demo/login`, `/demo/logout`, `/denied` (403).
- [x] 3. `/search` page with GET filters, paging, total, result cards.
- [x] 4. Top bar: persona + logout, Search link, Demo login link; resources el/en.
- [x] 5. Green: full `make test`. Record output.
- [x] 6. Screenshots: /demo, /search (desktop el, mobile el).
- [x] 7. Validate, commit, push, archive.

## Red run (task 1) — 2026-11-21

All 11 tests in `SearchAndLoginTests` fail (no /demo, /search, /denied); the 23 earlier tests pass.
```
Failed!  - Failed: 11, Passed: 23, Skipped: 0, Total: 34
```

## Green run (task 5) — 2026-11-21

```
make test → Passed!  - Failed: 0, Passed: 34, Skipped: 0, Total: 34
```
`/denied` returns 403 (curl).

## Screenshot review fixes (orchestrator, task 6)
- Generator produced 15 duplicate full names (e.g. two "Άννα Αλεξίου" side by side in search) → names re-drawn until unique; DB check: 0 duplicates.
- Female coaches/athletes labelled with masculine role → `Role_Coach_F` "Προπονήτρια", `Role_Athlete_F` "Αθλήτρια" (gender claim at login; coach gender inferred from the generator's first-name list).
- Demo login showed only club-less athletes (lowest ids) → showcase mix: women 5 with club + 3 looking, men 3 + 1, most seasons first.

## Known limits
- Greek name search is not accent-insensitive.
- Mobile top bar stacks (functional; compacted in the polish change).
