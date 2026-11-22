# wow-audit-fixes (hardening after the independent audit of `wow-release`, 2026-11-22)

Serves: M5 demo login (reset, persona claims), M2 coach search (match/search parsing), M1 athlete profile (share card, wrapped),
M6 Greek/English (register, plurals), plus the in-scope feed/loop/evidence/share/find packages of 2026-11-21.

## Driver
Independent audit (Clara) of branch `wow-release`: 5 major + 11 minor findings, each with evidence from her run
(local fake server, mutation tests, queries). Baseline before this change: `BALLPASS_TESTDB_SUFFIX=_fix dotnet test` = 332 passed, 0 failed (measured 2026-11-22).

## Findings (evidence as reported by the audit; the red run in tasks.md re-measures every behavioural one)
| # | Severity | Finding | Evidence |
|---|---|---|---|
| 1 | major | `POST /demo/reset` is anonymous, unthrottled, `Demo:Enabled` defaults true | anonymous POST with a token wipes all demo data (reproduced: existing test posts it with an anonymous client) |
| 2 | major | /match "top X%" uses a different cohort than the profile | FitScorer ranks per-game values among same-position athletes with >=10 games; Evidence ranks per-40 among same gender+position group with a minutes floor and n>=20 |
| 3 | major | `AnthropicClient` default `MaxRetries` = 2 | Clara's local fake server counted 3 requests for one 500 reply |
| 4 | major | Claude call robustness | schema has no property descriptions, nullable enums use `type:[..,"null"]` + `null` in enum, timeout 4 s, fallback reason never logged |
| 5 | major | Home feed strip ignores hide/block/report | `GetPreviewAsync(count)` has no viewer; `/feed` filters, `/` does not |
| 6 | minor | "πόιντ γκαρντ" parsed as PG+SG | `Guard` regex matches `γκαρντ` after the specific PG patterns miss the Greek spelling |
| 7 | minor | "κάτω των 16" gives minAge=18, maxAge=18 | 0 results (exact 18 only) with a misleading chip |
| 8 | minor | bad query values on /match and /search | `minHeight=abc`, `confirmed=maybe`, `q=%00`, `page=abc`... risk of 500 |
| 9 | minor | unsupported chip key mismatch | rule parser emits `stats`, AI prompt says "statistics"; the UI only labels `stats` |
| 10 | minor | wrapped cohort note omits position group and minutes floor; /compare warns "different positions" for different genders | wording |
| 11 | minor | mixed Greek register (singular "Δες/Ακολούθησε" vs formal plural), "1 αποτελέσματα", two relative-time implementations with different wording | Feed/Loop/Evidence/Find/Share resources |
| 12 | minor | share card always says "Ψάχνει ομάδα" for athletes without a club | independent of `LookingForTeam`; cache key unaffected |
| 13 | minor | `IsDemo` missing on Kudos/Follow/Block/Report; seeder uses `TimeProvider.System` | schema |
| 14 | minor | duplicates: antiforgery check x3, persona claims x2 (login endpoint vs `PersonaClaims`), relative time x2 | code |
| 15 | minor | surviving mutants N13 (wrapped boundary at 5 games) and N16 (self-follow) | mutation run: no test failed |
| 16 | minor | docs | DEMO_DATA.md lacks posts/kudos/follows/lifecycle; README lacks Loop row; `config.yaml` scope still lists "comments"; wow-share proposal TODO stale |

## Measured blast radius (2026-11-22)
| Item | Count |
|---|---|
| Existing tests | 332 (green) |
| Tests expected to change | reset tests (anonymous reset assumption), FitScorer fixtures (cohort ranks passed in), under-18 chip test, Greek wording asserts |
| Shared files edited | Program.cs, appsettings.json, appsettings.Development.json, BallpassDbContext (no), FeedEntities.cs, 5 Greek + 5 English resx |
| DB | 1 migration `WowAuditFixes`: `IsDemo` boolean on Kudos, Follow, Block, Report (existing rows default true) |
| New config | none required; `Anthropic:BaseUrl` (optional, tests and proxies), `Demo:Enabled` default flips to false |

## Decision
1. **Reset:** requires a signed-in persona (anonymous -> `/demo`), ASP.NET Core rate limiter policy `demo-reset` (fixed window, 1 permit / 60 s,
   partitioned by the persona's `NameIdentifier`; anonymous callers are not limited because the handler redirects them), rejection = 429 page.
   `Demo:Enabled` is false in `appsettings.json`, true in `appsettings.Development.json`; the class default is false too.
2. **Match = profile:** `ICohortRank` gains `PercentilesAsync(gender, metric)`; Match passes per-candidate `FitStatRank` (per-40 value + whole percent) into the pure `FitScorer`.
   null rank = criterion missing ("partial data") and no top-% reason.
3. **Client:** `MaxRetries = 0`; optional `Anthropic:BaseUrl`.
4. **Claude:** property descriptions, `anyOf` nullable schema, `Ai:TimeoutSeconds` 10, `AiFallbackReason` logged at Information (never the query text).
5. **Home strip:** `GetPreviewAsync(viewer, count)` shares the visibility filter with `GetFeedAsync`.
6-10. Parser/validator/page fixes, lenient query binding, one label key set, wrapped/compare wording.
11-14. Wording, plural forms, shared `RelativeTimeText`, share-card flag, `IsDemo` columns, `TimeProvider`, shared antiforgery helper, single `PersonaClaims`.
15-16. Two boundary tests, docs.

## Risks / limits (stated up front)
- The reset limiter is per persona. A reset deletes and re-creates every persona (new ids), so one visitor can still reset again by signing in as another persona. A global cap was not requested; recorded as accepted below.
- Anonymous visitors still see the reset link and the confirmation page (UI unchanged); pressing the button sends them to the persona chooser.

## Accepted, not in scope
- `?parsed=ai` in the URL can show the "AI" label although the rules answered (cosmetic).
- The per-persona AI call cap is held per process (not shared across instances).
