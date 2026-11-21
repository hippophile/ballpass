# Tasks — wow-evidence

- [x] 1. Stub types (signatures returning defaults) + failing `EvidenceTests` (66 tests). **Red run recorded.**
- [x] 2. StatAggregator, CohortCalculator/CohortService, BadgeService, ChartGeometry.
- [x] 3. Resources (EvidenceResource el/en), bp-evidence.css (chart tokens), EvidencePanel, Compare page, CompareLauncher, 3 seam lines + AddEvidence().
- [x] 4. Green: `BALLPASS_TESTDB_SUFFIX=_evidence dotnet test`. **Green run recorded.**
- [x] 5. Screenshots 1280 + 390 (profile, compare, shortlist) read and fixed.

## Red run (task 1)
`BALLPASS_TESTDB_SUFFIX=_evidence dotnet test --filter FullyQualifiedName~EvidenceTests` (assertion failures on stub defaults, 404s, missing css file)
```
Failed!  - Failed:    58, Passed:     8, Skipped:     0, Total:    66
```
Examples: `Mid_rank_counts_ties_as_half` Expected 38 Actual 0; `Compare_anonymous_is_sent_to_demo_login` Expected Found Actual NotFound;
`Profile_stays_200...` "data-evidence" not found; `Chart_tokens_meet_3_to_1` FileNotFound bp-evidence.css.

## Green run (task 4)
```
Passed!  - Failed: 0, Passed: 139, Skipped: 0, Total: 139   (73 existing + 66 new)
```

## Screenshots (scratchpad)
evidence-profile-1280.png, evidence-profile-390.png, evidence-compare-1280.png, evidence-compare-390.png, evidence-shortlist-390.png.
Fixed after reading: visually hidden table widened the page to 633px at 390 (wrapped in a clipping box); compare table overflowed (own scroll wrapper,
the profile's `.table-scroll` is scoped CSS); wrapped badges (block + rounded rect); concurrent DbContext 500 (own scope per component query).

## Deviations / notes
- No `MapEvidence()`: no endpoints. `AddEvidence()` lives in namespace Microsoft.Extensions.DependencyInjection so Program.cs needs one line only.
- Components live in Features/Evidence with `@namespace Ballpass.Web.Components`; folder has its own `_Imports.razor`.
- Position group = Guard{PG,SG} / Forward{SF,PF} / Center{C}. Peers exclude the athlete; ICohortRank n = peers + 1.
- Source label on the profile panel is a plain-text span (not SourceBadge) so LookTests' badge count stays equal to stat rows.
