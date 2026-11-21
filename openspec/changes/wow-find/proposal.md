# wow-find (Phase B "Find" of the wow release; serves M2 coach search, M3 express interest)

## Driver
`docs/research/wow-plan.md` P3a (Match) + P3b (AI search), amended by `docs/research/DECISIONS.md`:
fit engine + coach need form + ranked reasons; AI search (NL -> filters) with rule fallback and safety rail.
Region map: NOT in this change (later). Coach daily interest quota: NOT in this change.

## Measured blast radius (2026-11-21)
| Item | Count | Command |
|---|---|---|
| Shared files edited (one line each) | 4: `Search.razor` (2 lines), `MainLayout.razor` (1), `App.razor` (1 css link), `Program.cs` (2: `AddFind()`, `MapFind()`) | `git diff --stat` |
| New package | `Anthropic` 13.12.0 (official SDK), pinned | `dotnet add package` |
| Existing tests | 55 + Look tests, must stay green | `BALLPASS_TESTDB_SUFFIX=_find dotnet test` |
| Entities / migrations | 0 (no new tables; per-game stats already stored) | n/a |

## Decision
1. `FitScorer`: one pure deterministic class; weights from `Fit:*` (default weights, hand-chosen estimates, shown as such).
2. `/match` (coach-only): need form, hard filters (position, coach-confirmed-only) hide athletes and report the count,
   ranked results with templated reasons each carrying a SourceBadge, "partial data" tag, express-interest form (existing endpoint),
   stat trust bar (self-reported / coach-confirmed / league-verified (mock), sums to 100).
3. `INlQueryParser` -> `SearchFilters`. `RuleParser` (always available, Greek + English, accent-insensitive).
   `ClaudeParser` (official SDK, structured JSON output) behind a guard: `Ai:Enabled`, per-session cap, in-memory cache, timeout, kill switch.
   Every failure (refusal, max_tokens, invalid JSON, rate limit, timeout, exception) falls back to rules, no retry loop.
4. `GET /find/ai?q=..&to=search|match` redirects to `/search?...` or `/match?...` (works in static SSR). Filters render as editable chips.
5. The key lives ONLY in user-secrets / env `Anthropic__ApiKey`. A test fails if the Anthropic key prefix appears in a tracked file.
6. The fit score is never called AI. Labels: "Computed on fictional demo data with default weights".
