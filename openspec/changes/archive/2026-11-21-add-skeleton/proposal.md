# add-skeleton (MVP item M0)

## Driver
Founder (dev) asked to start the investor POC: "create the private repo etc and GO" (2026-11-21).
Plan: Ballpass — Full Analysis (Notion, Nov 2026) §8, build session 1 of 6.

## Why now
Nothing exists yet. Every later item (M1–M6) needs a running app, a database connection and the
demo banner. The banner is an ethics requirement (fictional demo data must be obvious on every page).

## Measured blast radius (2026-11-21)
| Item | Count |
|---|---|
| Existing source files in repo | 0 (fresh repo, only openspec/ and .claude/) |
| Existing databases named `ballpass` in local `postgres` container | 0 |
| Specs in `openspec/specs/` | 0 |

## Decision
1. `src/Ballpass.Web` — Blazor Web App, .NET 10, **static SSR** (`--interactivity None`): no per-tab
   server memory, shareable URLs.
2. `tests/Ballpass.Web.Tests` — xUnit + `Microsoft.AspNetCore.Mvc.Testing` integration tests.
3. EF Core + Npgsql: `BallpassDbContext` (no entities yet; schema is session 2).
   Connection string key `ConnectionStrings:Ballpass`, value from user-secrets locally, env var on the VM.
   `appsettings.json` holds no connection string.
4. `GET /health` — ASP.NET health checks with a DbContext check: 200 when the DB answers, 503 when not.
5. Demo banner in `MainLayout` (every page), Greek + English text, `role="status"`.
6. Remove template sample pages (Counter/Weather) — not MVP.
7. `Makefile`: `make setup` (restore, create `ballpass` DB in the existing `postgres` container,
   store the connection string in user-secrets without printing it), `make run`, `make test`.
8. `Ballpass.slnx` solution at repo root.

## Verification
- Red first: tests written before the endpoint/banner exist; failing run recorded in tasks.md.
- Green: `make test` passes.
- Negative case: with an unreachable DB, `/health` returns 503 (test).
- `git grep -i password` finds no secret in tracked files.
- Manual: `make run`, open the logged URL, screenshot shows the banner.

## Caveats
- Integration tests use the real local Postgres (`ballpass` DB). If the `postgres` container is down,
  the health-200 test fails — by design, it proves the real connection.
- No auth, no entities, no i18n framework yet (M5/M6 and session 2). Banner text is hardcoded GR+EN
  until M6.
