# wow-evidence (Phase B package "Evidence"; serves M1 athlete profile, M4 shortlist)

## Driver
Founder decisions (docs/research/DECISIONS.md, 2026-11-21): Evidence = percentile bars vs a demo cohort, season trend line,
compare 2 players. NO leaderboard, NO radar, NO overall rating, NO shot zones.

## Measured blast radius (2026-11-21, base wow-release 41330a0)
| Item | Count | Command |
|---|---|---|
| Existing tests (must stay green) | 73 (55 pre-Look + 18 LookTests) | `dotnet test` |
| Shared files edited | 4: AthleteProfile.razor (+1 line), Shortlist.razor (+1 line), App.razor (+1 `<link>`), Program.cs (+1 line `AddEvidence()`) | `git diff --stat` |
| Entities / migrations added | 0 (percentiles are computed from existing StatLine rows) | |
| Seed rows | stat lines pre-existing, multi-season already seeded (no seed change) | `--reset-demo-data` log |

## Decision
1. `Features/Evidence`: `StatAggregator` (per-game, per-40, latest + career, trend, season slots), `CohortService`
   (cohort = same gender + position group Guard{PG,SG} / Forward{SF,PF} / Center{C}; each peer's latest season;
   percentile = mid-rank clamped 1-99; >=20 peers and >=10 games else no fill + "Δεν υπάρχουν αρκετά δεδομένα (n=...)";
   optional coach-confirmed-only peers), `BadgeService` (6 rule badges), public `ICohortRank`.
2. `EvidencePanel` inserted into the profile (one line): percentile bars, season line + trend, badges, compare link for coaches.
3. `/compare?a=&b=` (coach-only) and `CompareLauncher` on /shortlist.
4. `wwwroot/css/bp-evidence.css` with `--bp-chart-a..e` (Okabe-Ito), contrast >= 3 on #0B0F14 asserted by a test.

## Caveats
- Thresholds (20 peers, 10 games, +-10%, 8 games) are our estimates, labelled so in the UI.
- Cohort is fictional and small: male centers (~16) fall under 20 peers by design; coach-confirmed-only shrinks most cohorts below 20.
- FG% career is a games-weighted mean (no attempts in the data).
- No endpoints are needed, so no `MapEvidence()` is added (only `AddEvidence()`).
