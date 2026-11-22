# Tasks — wow-audit-fixes

- [x] 1. Failing tests first (tests/Ballpass.Web.Tests: `AuditResetTests`, `AuditFindTests`, `AuditAiTests`, `AuditMiscTests`, edits in `FitScorerTests`, `AiSearchTests`, `EvidenceTests`, `FindPagesTests`, `LoopTests`) with minimal stubs. **Record red run** (below).
- [x] 2. Reset: signed-in persona, rate limiter policy `demo-reset` (1/min/persona), 429 page, `Demo:Enabled` false/true split, `DemoDataSeeder(TimeProvider)` + virtual `ResetAsync`.
- [x] 3. Match = profile: `ICohortService.PercentilesAsync`, `FitCandidate.StatRank`, scorer consumes it, Match wording (per 40, group label).
- [x] 4. `AnthropicClient` `MaxRetries = 0`, optional `Anthropic:BaseUrl`; schema descriptions + `anyOf`; timeout 10 s; `AiFallbackReason` logged at Information (no query text).
- [x] 5. Home strip: `GetPreviewAsync(viewer, count)`, shared `VisiblePostsAsync` filter.
- [x] 6-9. Rule parser PG spelling; under-18 clamp (minAge 18, maxAge open); `LenientQuery` on /search and /match; one unsupported key set (`stats`, `other`).
- [x] 10. Wrapped cohort note (group + minutes floor); compare gender warning.
- [x] 11. Formal plural Greek in Feed/Loop/Evidence/Find/Share; singular counts; `RelativeTimeText` shared by PostCard and Sent.
- [x] 12. Share card follows `LookingForTeam` (hash covers the club line).
- [x] 13. `IsDemo` on Kudos/Follow/Block/Report (migration `WowAuditFixes`, existing rows default true); seeder clock.
- [x] 14. Dedupe: `AntiforgeryCheck.IsValidAsync` (Program, Loop, Feed), `PersonaClaims` in `/demo/login`, `RelativeTimeText`.
- [x] 15. Boundary tests N13 (5 games) and N16 (self-follow); N05 test kills the "no sign-out" mutant (checked by mutating, see below).
- [x] 16. Docs: DEMO_DATA.md, README Loop row, `openspec/config.yaml` scope line, wow-share proposal.
- [x] 17. Green: full suite. **Record green run.**
- [x] 18. Screenshots of every page whose UI text changed, read.

## Accepted, not in scope (recorded per the founder's brief)
- The `?parsed=ai` URL can show the AI label although the rules answered (cosmetic).
- The per-persona AI call cap is held per process (not shared across instances).
- The reset limiter is per persona, and a reset re-creates every persona with new ids, so one visitor can still reset again as a different persona. A global cap was not requested.
- `FindSetup` has no antiforgery check to share (`/find/ai` is a GET), so the shared helper is used by Program, LoopSetup and FeedSetup.
- Anonymous visitors still see the reset link/confirmation page (UI unchanged); the button sends them to `/demo`.

## Red run (task 1) — 2026-11-22
`BALLPASS_TESTDB_SUFFIX=_fix dotnet test` against stubs (seeder ctor + virtual, `FitStatRank`, `PercentilesAsync` returning empty, `GetPreviewAsync(viewer)` ignoring the viewer, `AiFallbackReason` without logging, `UnsupportedKeys` empty, `RelativeTimeText` returning "", `IsDemo` columns never set):
```
Failed!  - Failed: 74, Passed: 356, Skipped: 0, Total: 430
```
All 74 fail on assertions (none on compile or missing-column errors). Reasons measured:
| Finding | Red assertion |
|---|---|
| 1 reset | anonymous reset: Location `/?demo=reset` not `/demo`; second reset by same persona: `Found` not `TooManyRequests`; shipped `Demo:Enabled` = true |
| 2 match | centers: `top 2% των Cohort_Cen…` shown although the profile has no percentile; forwards: reason `Range (15 - 17) Actual: 1`; batch percentiles: key not present |
| 3 retries | `Expected: 1 Actual: 3` requests for one HTTP 500 (Clara's measurement reproduced) |
| 4 claude | `gender has no description`, `gender: type array`, timeout `Expected 10 Actual 4`, 6 tests "log collection was empty" |
| 5 strip | `DoesNotContain` failed: blocked/hidden author still in the home strip |
| 6/7 parser | `["PG","SG"]` for "πόιντ γκαρντ"; `Expected null Actual 18` (maxAge) |
| 8 bad values | 31 of 34 cases `Expected OK Actual InternalServerError` (minHeight/minAge/maxAge/page=abc, looking/confirmed=maybe, huge numbers, `q=%00`, `region=%00`) |
| 9 keys | `UnsupportedKeys` empty; prompt said "statistics" |
| 10 | warning for female vs male read "διαφορετικές θέσεις (Πόιντ γκαρντ…)"; wrapped note lacked "φόργουορντ" |
| 11 | 5 register tests list 31 singular forms; relative time `""`; Sent page "πριν 12 ημ."; `1 αποτέλεσμα` missing |
| 12 | club line "Χωρίς ομάδα · Ψάχνει ομάδα" for a non-looking athlete; ETag unchanged after toggle |
| 13 | seeded kudos/follows `IsDemo` false; seeder stamped 2026-10 posts for a fixed 2030 clock (40 of 40 out of range) |
Green on purpose at the red run: N05 `Reset_signs_the_caller_out_even_when_the_persona_still_exists`, N13 boundary tests, N16 self-follow (the code was already right; the tests are what the audit lacked).
N05 mutation check: removing `SignOutAsync` from `/demo/reset` -> that test fails (`Sequence contains no matching element`: no Set-Cookie clearing the auth cookie); code restored.

## Green run (task 17) — 2026-11-22
`BALLPASS_TESTDB_SUFFIX=_fix dotnet test` (whole suite)
```
Passed!  - Failed: 0, Passed: 430, Skipped: 0, Total: 430 - Ballpass.Web.Tests.dll (net10.0)
```
(332 before + 98 new test cases; existing tests edited only where the spec changed the behaviour or the wording, see the report.)

## Screenshots (task 18) — own app, port 5286, `ASPNETCORE_ENVIRONMENT=Development`, DB `ballpass_fix_dev`
Read: feed (tabs, buttons, "πριν από"), home strip + toast, /me/sent, /me/inbox (pill, accepted text), /demo/reset, 429 page (produced by two concurrent POSTs: one 429, one reset), /match forwards ("26,7 ριμπ./40΄ — top 1% των φόργουορντ (δείγμα επίδειξης)"), /match centers ("Μερικά δεδομένα", no top-%), /search under 16 (chips "Ηλικία από: 18" + minors, 200 results), /compare female vs male warning, Season Wrapped cohort slide, profile share panel.


## Orchestrator review after the fix round — 2026-11-22
- **MinPeers was a hardcoded 20** (config rule: no hardcoded thresholds) and left NO female center with a percentile (19 qualify),
  removing the core demo moment. Now `Evidence:MinPeers` (default 15, an estimate) in appsettings; every text that said "20"
  renders the configured value. Red: `Female_center_profile_shows_percentiles_with_default_config_and_the_threshold_text`
  ("λιγότεροι από 20 συνομήλικοι" found) → green.
- Threshold tests: 2 pure tests use `CohortCalculator.DefaultMinPeers - 1`; 2 DB tests keep their original scenario by setting
  `Evidence:MinPeers=20` via config (also proves the setting works). The premise-false "female centers have no percentile"
  test was replaced by running the match↔profile consistency test for centers as well as forwards ([Theory]).
- That consistency test for centers exposed a TEST bug, not a product bug: its regex `top (\d+)%` matched inside the
  below-average text "κάτω από το top 50%". Diagnostic (throwaway test, deleted) showed profile = batch = 44th percentile.
  Regex now `— top (\d+)%`.
- Full suite: 431/431, three consecutive runs (≈24 s each), no hang. `make test` now uses `--blame-hang-timeout 5m`.
