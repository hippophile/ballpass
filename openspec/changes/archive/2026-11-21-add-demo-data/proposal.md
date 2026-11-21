# add-demo-data (supports MVP items M1 profile, M2 search)

## Driver
Build session 2 of 6 (Ballpass — Full Analysis §8, "Plan to fill it", phase A). Founder: "please continue" (2026-11-21).
Women's basketball first; men switchable.

## Why now
M1 (profile) and M2 (search) need realistic data to show investors. Real people are not allowed in the demo
(ethics rules in the analysis §8), so the data is generated, deterministic and flagged.

## Measured blast radius (2026-11-21)
| Item | Count |
|---|---|
| C# files in repo | 3 (Program.cs, BallpassDbContext.cs, SkeletonTests.cs) |
| Entities in `BallpassDbContext` | 0 |
| EF migrations | 0 |
| Tables in local `ballpass` DB | 0 |

## Decision
1. Entities in `src/Ballpass.Web/Data/`:
   - `Club`: Id, Name (ends with " (demo)"), City, Region, Gender (Women/Men), League (A1, A2, B, Regional), IsDemo.
   - `Athlete`: Id, Slug (unique, Latin), FirstName, LastName (Greek, gender-correct surname form), FirstNameLatin, LastNameLatin,
     BirthYear, Gender, Position (PG/SG/SF/PF/C), HeightCm, City, Region, CurrentClubId (nullable),
     LookingForTeam, Bio, VideoUrl (null in demo data — no real people's video), IsDemo.
   - `AthleteSeason`: AthleteId, ClubId, Season (e.g. "2025-26") — club history.
   - `StatLine`: AthleteId, Season, League, Games, Minutes, Points, Rebounds, Assists, Steals, Blocks (per-game averages),
     FgPct, ThreePct, FtPct, Source (SelfReported / CoachConfirmed / LeagueVerifiedMock).
   - `Coach`: Id, FirstName, LastName, Latin names, ClubId, Role (Head/Assistant), Region, IsDemo.
2. EF migration `InitialDemoSchema`. Migrations are applied by `make setup` / `make reset` (the `--reset-demo-data`
   path), **not** on web startup: startup migration would crash the app when the DB is down and break the
   `/health` 503 behaviour from add-skeleton. (Corrected 2026-11-21 before implementation.)
3. `DemoDataGenerator` (pure, no DB): Bogus with fixed seed 42 + own Greek name lists. Output:
   200 women + 100 men athletes, 20 clubs (12 women, 8 men), 30 coaches, 1–3 seasons each with stat lines.
   All adults: age 18–34 on 2026-01-01. Stats drawn per position and league within plausible ranges.
4. `DemoDataSeeder`: wipes demo rows and inserts the generated set in one transaction (idempotent reset).
5. `make reset` → runs the app with `--reset-demo-data`: migrate, reseed, exit. No HTTP reset endpoint yet (needs auth, M5).
6. Stat `Source` shows the planned trust labels; "LeagueVerifiedMock" is explicitly a mock — no partner name.

## Verification
- Red first: generator + seeder tests written before the code.
- Generator tests (no DB): counts, determinism (same seed → identical output), all IsDemo, all ≥ 18, club names end
  "(demo)", unique slugs, every athlete ≥ 1 stat line, stats within ranges, women surnames in female form.
- Seeder tests (real Postgres, separate `ballpass_test` DB): counts after reset; reset twice → same counts (not doubled).
- `make reset` on the dev DB; `SELECT count(*)` per table recorded.
- Skeleton tests stay green.

## Caveats
- Names are random combinations of common Greek names; a combination can match a real person by chance.
  Mitigation: every page has the demo banner, every row IsDemo, clubs carry "(demo)". Stated in DEMO_DATA.md.
- Stats are plausible, not real distributions (estimate). No minors exist in the data at all (search rule for 15–17 is later work).
- Tests need the local `postgres` container running.
