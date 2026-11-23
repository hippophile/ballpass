# Tasks — flow-release

## WP-A Foundations (merges first)

- [x] A1. Red tests (record the red run below): LookTests (§3 contrast table ±0.01, scrim over white, primitives, shell width,
      nav counts, banner absent + footer disclosure, no demo words in visible text), StockMediaTests (slots/pools/credits/M2/O4,
      StockPhoto + HeroVideo markup, Demo:Featured config), SkeletonTests (footer instead of banner), and every existing test that
      asserted a demo label rewritten to assert its absence (DemoDataGenerator, Evidence, Find, Feed, Engage, Media, Loop, Share,
      Profile, AuditFix).
- [x] A2. Tokens (`bp-tokens.css` §3 diff) + `bp-base.css` primitives and type scale.
- [x] A3. Shell: `MainLayout.razor(.css)` one 1280 px width, logo aligned, coach 3 / athlete 2 nav links, `bp-usermenu` with jersey
      avatar, persona switch visible, EL|EN, banner removed, footer disclosure; `LoopNavLink`, `MatchNavLink`.
- [x] A4. O1 across the app: resx (Shared, Feed, Engage, Evidence, Find, Media, Loop, Share), DemoDataGenerator club suffix,
      FeedSeeder place suffix, OG card text, chips/notes removed from PostCard, MediaTile, StoriesBar, Stories, ProfileHighlights,
      FeedPage, FeedPreview, EngageBelowTabs, AthleteProfile, Inbox, Season, Home caption/footer.
- [x] A5. Media: 30 stock files + `credits.json`, `docs/ASSETS.md` rows, `StockMedia.cs`, `StockPhoto.razor`, `HeroVideo.razor`,
      `bp-media.css` rules.
- [x] A6. Config: `Demo:Featured` (empty defaults in appsettings.json, values in appsettings.Development.json; NeedQuery left for WP-B).
- [x] A7. Green: `BALLPASS_TESTDB_SUFFIX=_flowa dotnet test --blame-hang-timeout 5m` (full suite once), `dotnet build` 0 warnings.
- [x] A8. Screenshots 1512 + 390 of `/`, `/demo`, `/feed`, `/match`, `/p/danai-bakali`, read; content left = logo left (±1 px);
      scrollWidth ≤ 390; rendered visible text has no demo words except footer and `/demo` intro.

## WP-B Coach path (worktree from 2494aea)

- [x] B1. Red tests (record below): FindPagesTests (featured need ranks the featured athlete first at 100 with no tie, `?need=featured`,
      no "–" ring and the prompt card without a need, one `.bp-btn-primary` on the top card, 12 + "Περισσότερα" `take=24`, /search reuses
      the card, 3 AI example chips), ProfileTests (cover + teal chip + proof row, 3 featured highlight clips, every media tile has a picture,
      coach rail with one primary and the share card inside a closed `<details>`, anonymous 0 primaries), EvidenceTests (no season line
      under 2 seasons, bars in a 2-column grid), InterestShortlistTests (sent rail + switch form, shortlist grid with one primary, sent pill,
      empty band), StockMediaTests featured slug line (O7).
- [x] B2. `/match`: glow, need chip bar, AI box with 3 examples, filters sticky / "Φίλτρα (n)" details, compact results bar, top match card,
      `AthleteMatchCard` (top / match / search / shortlist), prompt card, `?need=featured`, `?take`. `/search` uses the card grid.
- [x] B3. Profile: cover + identity, proof row, highlights mosaic (featured: 3 stock clips, O9), stats card (dense evidence), history + bio card,
      sticky action rail (sent state with switch), 390 bottom bar (primary, ✎ note, "⋯" sheet).
- [x] B4. Shortlist: card grid + sticky compare panel + empty photo band.
- [x] B5. Green + screenshots.

Config (first pass, replaced by B7): `AthleteSlug = stavroula-ioannidou`, `NeedQuery = position=SG&minHeight=174&maxHeight=177&minAge=22&maxAge=28&region=Crete`.

### Red run (B1) — 2026-11-22
`BALLPASS_TESTDB_SUFFIX=_flowb dotnet test --filter "<FindPages|Profile|Evidence|InterestShortlist|StockMedia|Share|FitScorer|AuditFind|Loop|Media>Tests"`
(no stubs needed; assertion failures only):
```
Failed!  - Failed:    19, Passed:   253, Skipped:     0, Total:   272
  StockMediaTests.Featured_development_values_name_seeded_personas   Expected: "stavroula-ioannidou"  Actual: "danai-bakali"
  FindPagesTests.Featured_need_ranks_the_featured_athlete_first      Expected: "stavroula-ioannidou"  Actual: "danai-bakali"
  FindPagesTests.Without_a_need_there_are_no_rings_and_a_prompt_card  DoesNotContain() Failure: Sub-string found
  FindPagesTests.Match_has_one_primary_cta_on_the_top_card_...        Expected: 1  Actual: 0
  FindPagesTests.Match_shows_twelve_then_more_with_take               Expected: 13  Actual: 20
  FindPagesTests.Search_reuses_the_card_..., Ai_box_offers_three_example_chips_to_match (Expected: 3 Actual: 0)
  ProfileTests: cover/proof row, featured highlights, coach rail (Expected: 1 Actual: 0), anonymous share details
  EvidenceTests.Single_season_athlete_has_no_trend_chart              DoesNotContain() Failure: Sub-string found
  EvidenceTests.Percentile_bars_sit_in_a_two_column_grid              Pattern not found
  InterestShortlistTests: sent rail, shortlist grid, sent pill, empty band, Interest_sent_twice (data-rail="sent" missing)
```

### Green run (B5) — 2026-11-22
`dotnet build`: 0 warnings, 0 errors. Full suite once: `BALLPASS_TESTDB_SUFFIX=_flowb dotnet test --blame-hang-timeout 5m`
```
Passed!  - Failed:     0, Passed:   585, Skipped:     0, Total:   585, Duration: 34 s
```
Visual, app on :5302 against `ballpass_flowb_dev`, signed in as the O7 coach (Αικατερίνη Φωτοπούλου): `/match?need=featured`, `/match`,
`/p/stavroula-ioannidou` (+ `?status=interest-sent`), `/shortlist`, `/search?looking=true` at 1512 and 390, full page. Measured:
profile height at 1512 = 1979 px (≤ 2000); scrollWidth = 390 on every 390 route; `.bp-btn-primary` count = 1 on /match, /p, /shortlist
(0 on the sent state, where the primary is the teal-outline switch); 390 profile bottom bar fixed at 779–844 px (in view).

### Lead review follow-ups (B6–B7)

- [x] B6. Data realism: `DemoDataGenerator.NewStatLine` scales a line down (one factor for pts/reb/ast/stl/blk) until it is within the per-40
      caps pts 32, reb 20, ast 12, stl 5, blk 4; applied after all draws, so seed 42 is deterministic and in-cap lines are unchanged.
      Test first: `DemoDataGeneratorTests.No_stat_line_exceeds_the_per_40_cap` (+ in-cap lines keep their numbers).
- [x] B7. Featured athlete swapped to `ioanna-papageorgiou` (SG, 178, Crete, 23, 2024-25: 31.5 min, coach-confirmed, unchanged by B6).
      `NeedQuery = position=SG&minHeight=178&maxHeight=182&minAge=21&maxAge=26&region=Crete` → she is #1 at 100 %, next 95 %
      (stavroula-ioannidou, 177 cm). Highlights follow the config slug (O9). ProfileTests now also lock 2 percentile pills + drawn bars.

Red (B6): `dotnet test --filter DemoDataGeneratorTests`
```
Failed!  - Failed:     5, Passed:     9, Total:    14
  Points/40 > 32: 103 lines (e.g. panagiota-chatzidaki 2024-25 44.0)   Rebounds/40 > 20: 65   Assists/40 > 12: 45
  Steals/40 > 5: 60   Blocks/40 > 4: 28
```
Red (B7, config still on the old slug): `Failed: 3, Passed: 48` — StockMediaTests / FindPagesTests expected "ioanna-papageorgiou",
actual "stavroula-ioannidou"; ProfileTests featured highlights missing.

Tests whose pinned numbers changed because the data legitimately changed: only `ProfileTests.Profile_has_a_cover_identity_proof_row...`
(pinned "15,5" ppg of the old featured line, 8.2 min, which B6 scales down); it now reads the expected value from the generated data.
No other test pinned a capped line.

Green: `BALLPASS_TESTDB_SUFFIX=_flowb dotnet test --blame-hang-timeout 5m` → `Passed: 591, Failed: 0`; build 0 warnings.
Visual (dev DB reset after B6): `/match?need=featured` top card = ioanna-papageorgiou 100 %; `/p/ioanna-papageorgiou` height 1721 px at 1512,
scrollWidth 390 at 390; proof pills "Ριμπάουντ / 40΄: 40ο εκατοστημόριο", "Ευστοχία σουτ (FG%): 29ο εκατοστημόριο"; 5 percentile bars drawn.

## WP-C Front door, athlete path, feed

- [x] C1. Red tests: home doors post `/demo/login` with the right returnUrls; counters equal DB counts; `/demo` featured cards + ≤ 8 rows;
      feed 0 empty tiles and 240/600/320 grid; inbox accept is `bp-btn-athlete`; sent stepper orange, orange, teal.
- [x] C2. Home hero (HeroVideo), doors (`PersonaDoor`), connection band, counters, recent connections, activity photo headers, doors again.
- [x] C3. `/demo` featured row + persona lists; inbox and sent restyle; not-found band.
- [x] C4. Feed 3-column grid, rails (me card + links; "Ψάχνουν ομάδα τώρα" + trends ≤ 5), PostMedia pool fallback (graphics skipped).
- [x] C5. Green + screenshots (lead reads).

### Red run (C1) — 2026-11-22
`BALLPASS_TESTDB_SUFFIX=_flowc dotnet test --filter "<Home|SearchAndLogin|Loop|Feed|FeedLayout|Engage|Media>Tests"`, with a compile-only
stub `FlowResource` (empty resx), so every failure is an assertion, not a build error:
```
Failed!  - Failed:    24, Passed:   182, Skipped:     0, Total:   206
  HomeTests (10): doors   Assert.Equal() Expected: 2 (door forms)        hero  Pattern not found <video ... autoplay
                  counters FormatException (no data-count="looking")    Flow resource  Collection was empty
                  english  "Find a player" not found                     404   stock photo not found
  SearchAndLoginTests (4): featured cards Expected: 2 · lists Collection was empty · featured athlete → /me/inbox Strings differ
  LoopTests (3): accept button class bp-btn-athlete not found · stepper bp-step-coach not found · empty-state photo not found
  FeedLayoutTests (4): "grid-template-columns: 240px minmax(0, 600px) 320px" not found · left rail missing
  EngageTests (1): "Ψάχνουν ομάδα τώρα" not found       MediaTests (2): "video/gfx-" found in the feed · no eager image
```

### Green (C5) — 2026-11-22
`dotnet build`: 0 warnings, 0 errors. Full suite, once: `BALLPASS_TESTDB_SUFFIX=_flowc dotnet test --blame-hang-timeout 5m`
```
Passed!  - Failed:     0, Passed:   589, Skipped:     0, Total:   589
```
Visual, app on :5303 against `ballpass_flowc_dev` (Brave headless, read): `/`, `/demo`, `/feed` (anonymous, coach), `/me/inbox`
(pending, accepted), `/me/sent` (pending + accepted), 404, at 1512 and 390. scrollWidth = 390 on every 390 shot. Home at 1512×900:
both doors end at y = 887 (above the fold). `/feed` grid computes to `240px 600px 320px`, 0 media blocks without `img[src]`/`video[poster]`.
Hero video after a full load and after enhanced navigation from /feed: `paused=false`, `currentTime > 3`, `muted=true`.

## Red run (A1) — 2026-11-22
`BALLPASS_TESTDB_SUFFIX=_flowa dotnet test --filter "<Look|StockMedia|Skeleton|DemoDataGenerator|Evidence|FindPages|AuditFind|Feed|Engage|Media|Loop|Share|Profile|AuditFix|AuditMisc>Tests"`,
with compile-only stubs for `StockMedia`, `StockPhoto`, `HeroVideo`, `DemoOptions.Featured` (assertion failures, not build errors):
```
Failed!  - Failed:    83, Passed:   355, Skipped:     0, Total:   438
  --bp-text on --bp-bg = 16.45, spec 18.18          (Token_contrast_matches_the_spec_table, 9 rows)
  --bp-faint missing / --bp-athlete missing / --bp-accent-hi missing
  / (el) shows a demo label: " Περιβάλλον επίδειξης — όλοι οι αθλητές, προπονητές και σύλ"   (Visible_text_..., 16 rows)
  Every_route_is_dark_has_no_banner_and_one_footer_disclosure (8 routes), Coach_nav_has_three_primary_links..., Athlete_nav_has_two...
  StockMediaTests: 10 failing (credits.json missing, pools empty, Featured config empty, StockPhoto/HeroVideo markup)
  DemoDataGeneratorTests: 20 of 20 club names contain "(demo)"
  Evidence/Find/AuditFind/Feed/Engage/Media/Loop/Share/Profile/AuditFix/AuditMisc: label still present
```

## Green run (A7) — 2026-11-22
`dotnet build`: 0 warnings, 0 errors. Full suite, once: `BALLPASS_TESTDB_SUFFIX=_flowa dotnet test --blame-hang-timeout 5m`
```
Passed!  - Failed:     0, Passed:   569, Skipped:     0, Total:   569, Duration: 32 s
```
Visual (A8), app on :5301 against `ballpass_flowa_dev`: logo left = content left at 1512 on `/`, `/demo`, `/feed`, `/match`,
`/shortlist`, `/p/danai-bakali`, `/me/inbox` (148 = 148, content 1216); at 390 (16 = 16, content 358) and scrollWidth = 390 on every route shot.
Visible-text grep (anonymous, el + en): 0 hits outside the footer except the allowed `/demo` intro sentence.
