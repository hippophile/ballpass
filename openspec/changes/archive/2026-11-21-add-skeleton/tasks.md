# Tasks — add-skeleton (M0)

- [x] 1. Scaffold `src/Ballpass.Web` (Blazor, .NET 10, interactivity None) + `tests/Ballpass.Web.Tests` (xUnit) + `Ballpass.slnx`; remove sample pages. Build green.
- [x] 2. Write failing tests: health 200, health 503 (bad port), banner on `/`, banner on unknown path, no connection string in tracked appsettings. **Run and record the red output below.**
- [x] 3. Create `ballpass` DB in local `postgres` container; store connection string in user-secrets (never printed).
- [x] 4. Implement: `BallpassDbContext`, health checks with DbContext check at `/health`, demo banner in `MainLayout` + CSS.
- [x] 5. `Makefile` with `setup`, `run`, `test`.
- [x] 6. Green: `make test` passes. Record output below.
- [x] 7. Verify: `git grep -i password` clean; `make run` + screenshot of banner.
- [x] 8. `openspec validate add-skeleton`, commit, push, archive.

## Red run (task 2) — 2026-11-21

```
Failed Every_page_shows_the_demo_banner(path: "/")
Failed Every_page_shows_the_demo_banner(path: "/this-page-does-not-exist")
Failed Health_returns_503_when_database_unreachable   Expected: ServiceUnavailable  Actual: NotFound
Failed Health_returns_200_Healthy_when_database_reachable   Expected: OK  Actual: NotFound
Failed!  - Failed: 4, Passed: 1, Skipped: 0, Total: 5
```
The 1 pass is the guard `Tracked_appsettings_contain_no_Ballpass_connection_string` (holds from the start; it protects against regressions).

## Green run (task 6) — 2026-11-21

```
make setup  → database ballpass created; ConnectionStrings:Ballpass stored in user-secrets (password never printed)
make test   → Passed!  - Failed: 0, Passed: 5, Skipped: 0, Total: 5
```

## Verification (task 7)
- Secret scan of tracked files: only the deliberately fake `Password=x` in the 503 test; the Makefile reads the password into a variable.
- `make run` → `GET /health` = `Healthy (200)`; desktop screenshot shows the GR+EN banner; unknown path shows Not Found + banner.
- Note: the 390px headless screenshot clips on the right (possibly a headless min-width artifact). Mobile layout is planned for M6.
