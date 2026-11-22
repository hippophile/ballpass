## 1. Tests first
- [x] 1.1 LoopTests (suffix _loop) written against compile-only stubs (migration WowLoop, Interest columns, stub seeder, minMinutes parameter ignored).
  RED (2026-11-22, `BALLPASS_TESTDB_SUFFIX=_loop`, `--filter LoopTests`): `Failed!  - Failed: 27, Passed: 2, Total: 29`
  Causes are assertion failures (values differ, sub-string not found, wrong status code, null SeenAt); the 2 passes are the
  "peer at exactly the floor" boundary and the composer hint test (the code has no placeholder attribute, so that already held).
  Full output: scratchpad loop-red.txt.

## 2. Implementation
- [x] 2.1 Integration fixes: Evidence minutes floor (`Evidence:MinMinutesPerGame`), Wrapped via `ICohortRank` (SeasonCohort deleted),
  compact action bar, search pager params. Composer hint: already single (no placeholder in code), locked by a test.
- [x] 2.2 Interest lifecycle, migration WowLoop, deterministic seed (12 seen, 6 replied of 40).
- [x] 2.3 /me/sent, reply endpoint, persona switch, reset.

## 3. Verification
- [x] 3.1 GREEN: LoopTests `Passed! - Failed: 0, Passed: 29`; full suite `Passed! - Failed: 0, Passed: 330, Total: 330` (twice).
- [x] 3.2 Screenshots read (scratchpad loop-*-1280.png, loop-*-390.png): profile action bar, sent (before/after), inbox accepted,
  header pill (desktop + mobile menu), reset confirm, Wrapped cohort slide. scrollWidth == viewport width on every page at 1280 and 390.
  Fixed after reading: screenshots caught the page-entry fade, so a 1 s wait was added to the capture script (no CSS change).
- [x] 3.3 `openspec validate wow-loop`: valid. Not archived.
