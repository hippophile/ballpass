# Tasks — add-demo-data (supports M1, M2)

- [x] 1. Write failing tests: `DemoDataGeneratorTests` (no DB) and `DemoDataSeederTests` (Postgres `ballpass_test`). Minimal type stubs only so tests compile. **Record red run.**
- [x] 2. Entities + `BallpassDbContext` mapping (unique slug index, enums as strings) + migration `InitialDemoSchema`.
- [x] 3. Greek name lists (female/male first names, surname pairs male/female, Latin transliteration) + `DemoDataGenerator` (Bogus, seed 42).
- [x] 4. `DemoDataSeeder` (migrate + transactional wipe + insert) + `--reset-demo-data` command-line switch (no migration on normal web startup).
- [x] 5. `make reset`; `DEMO_DATA.md` (what is fictional, how to reset, the name-collision caveat).
- [x] 6. Green: full `make test`. Record output.
- [x] 7. `make reset` on dev DB; record table counts.
- [x] 8. Validate, commit, push, archive.

## Red run (task 1) — 2026-11-21

Compile-only stubs (`DemoDataGenerator.Generate`, `DemoDataSeeder.ResetAsync` throw `NotImplementedException`; `GreekNames.Surnames` empty).
```
Failed DemoDataGeneratorTests.Adults_only
Failed DemoDataGeneratorTests.Same_seed_gives_identical_athletes
Failed DemoDataGeneratorTests.Expected_volumes
Failed DemoDataGeneratorTests.Women_have_female_surname_forms
Failed DemoDataGeneratorTests.Everything_is_flagged_demo_and_clubs_are_labelled
Failed DemoDataGeneratorTests.All_three_stat_sources_are_used
Failed DemoDataGeneratorTests.Slugs_are_unique_lowercase_latin
Failed DemoDataGeneratorTests.Heights_and_stats_are_plausible
Failed DemoDataSeederTests.Reset_twice_gives_exact_counts_not_doubled
Failed!  - Failed: 9, Passed: 5, Skipped: 0, Total: 14
```
The 5 passes are the add-skeleton tests (unchanged).

## Green run (task 6) — 2026-11-21

```
make test → Passed!  - Failed: 0, Passed: 14, Skipped: 0, Total: 14
```

## Dev DB after `make reset` (task 7)

```
Demo data reset: 20 clubs, 30 coaches, 300 athletes, 568 seasons, 568 stat lines
women 200 | looking for team 93 | non-demo rows 0 | under 18: 0
stat sources: SelfReported 342, CoachConfirmed 139, LeagueVerifiedMock 87
```
Second reset → identical counts (idempotent).

## Review fixes by orchestrator
- Club names used the nominative city ("Ηχώ Πειραιάς"); Greek club names use the genitive. Added `GreekCity.Genitive`, now "Ηχώ Πειραιά", "Μέλισσες Πάτρας".
- Nicknames: "Αετοί του Ανέμου" → "Αετοί", typo "Ζεφύροι" → "Ζέφυροι".

## Deviations accepted
- Percentages capped at 99.9 (numeric(4,1)).
- Athletes without a current club have no 2025-26 season.
