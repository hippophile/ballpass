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

## WP-B Coach path (placeholder, separate worktree after WP-A)

- [ ] B1. Red tests: featured NeedQuery ranks `danai-bakali` first; no "–" ring with a need; one `.bp-btn-primary` on /match, /p, /shortlist;
      0 empty media tiles on the profile; no trend chart under 2 seasons; share card inside closed `<details>`; interest-sent switch form.
- [ ] B2. `/match` header chip bar, top match card, `AthleteMatchCard`, prompt card without a need; `/search` reuses the card.
- [ ] B3. Profile cover, proof row, highlights, action rail (+ sent state), 390 bottom bar.
- [ ] B4. Shortlist grid + compare panel + empty state.
- [ ] B5. Green + screenshots (lead reads).

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
