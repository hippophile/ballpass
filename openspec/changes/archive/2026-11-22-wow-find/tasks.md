## 1. Tests first
- [x] 1.1 Write FitScorerTests, AiSearchTests, FindPagesTests against compile-only stubs; record the red run.
  RED (2026-11-21, `BALLPASS_TESTDB_SUFFIX=_find`, stubs returning defaults): `Failed! - Failed: 90, Passed: 4, Total: 94`
  (assertion failures: wrong order/percentages, empty filters, 404 on /match, no chips). Full output: scratchpad find-red.txt.

## 2. Implementation
- [x] 2.1 FitScorer + TrustBar (Features/Find/Fit). 3 hand-computed fixtures reproduced on first green run.
- [x] 2.2 RuleParser, SearchFilterValidator, NlText, ClaudeParser, GuardedNlQueryParser, FindRedirect, AnthropicClaudeClient (Features/Find/Ai).
- [x] 2.3 /find/ai endpoint, /match page, AiSearchBox, MatchLink, MatchNavLink, FindResource (el/en), bp-find.css.
- [x] 2.4 Seams: Search.razor (2 lines), MainLayout (1), App.razor (1), Program.cs (2), appsettings (Fit, Ai).

## 3. Verification
- [x] 3.1 GREEN: `Passed! - Failed: 0, Passed: 167, Total: 167` (full suite, suffix _find).
- [x] 3.2 Screenshots read: scratchpad find-match-view.png, find-match-2.png (1280), find-search-390.png (390).
  Fixed after reading: reason rows wrapped badly (text basis 14rem -> 8rem), ring label overflowed the ring (removed; aria-label kept),
  one-sided accent border removed per design review.
- [x] 3.3 `openspec validate wow-find`: valid. Not archived.
