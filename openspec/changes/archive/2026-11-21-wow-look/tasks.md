# Tasks — wow-look

- [x] 1. Failing `LookTests` (tests/Ballpass.Web.Tests/LookTests.cs). **Record red run.**
- [x] 2. Tokens + base CSS, `App.razor` (data attrs, CSS order), fonts + OFL files.
- [x] 3. Restyle all pages, `card`/`badge` to `bp-card`/`bp-badge`, source badge component with icon.
- [x] 4. Green: `BALLPASS_TESTDB_SUFFIX=_look dotnet test`. **Record green run.**
- [x] 5. Screenshots 1280x900 + 390x844 (home, profile, search, demo), read, fix; scrollWidth check; Greek tonos check.

## Red run (task 1) — 2026-11-21
`BALLPASS_TESTDB_SUFFIX=_look dotnet test --filter FullyQualifiedName~LookTests` (assertion / missing-file failures, not compile errors; 55 existing tests untouched)
```
Failed Ballpass.Web.Tests.LookTests.Text_tokens_meet_4_5_on_all_surfaces
  Failed Ballpass.Web.Tests.LookTests.Every_route_is_dark_and_keeps_the_demo_banner(path: "/search", persona: "coach")
  Failed Ballpass.Web.Tests.LookTests.Every_route_is_dark_and_keeps_the_demo_banner(path: "/shortlist", persona: "coach")
  Failed Ballpass.Web.Tests.LookTests.Every_route_is_dark_and_keeps_the_demo_banner(path: "/demo", persona: null)
  Failed Ballpass.Web.Tests.LookTests.Every_route_is_dark_and_keeps_the_demo_banner(path: "/p/SLUG", persona: null)
  Failed Ballpass.Web.Tests.LookTests.Every_route_is_dark_and_keeps_the_demo_banner(path: "/does-not-exist", persona: null)
  Failed Ballpass.Web.Tests.LookTests.Every_route_is_dark_and_keeps_the_demo_banner(path: "/", persona: null)
  Failed Ballpass.Web.Tests.LookTests.Every_route_is_dark_and_keeps_the_demo_banner(path: "/denied", persona: null)
  Failed Ballpass.Web.Tests.LookTests.Every_route_is_dark_and_keeps_the_demo_banner(path: "/me/inbox", persona: "athlete")
  Failed Ballpass.Web.Tests.LookTests.Design_css_loads_after_bootstrap
  Failed Ballpass.Web.Tests.LookTests.Source_badges_have_icon_and_text_and_league_says_mock(culture: "el")
  Failed Ballpass.Web.Tests.LookTests.Source_badges_have_icon_and_text_and_league_says_mock(culture: "en")
  Failed Ballpass.Web.Tests.LookTests.Ui_border_meets_3_on_bg_s1_s2
  Failed Ballpass.Web.Tests.LookTests.Pages_and_css_reference_no_external_hosts(path: "/demo")
  Failed Ballpass.Web.Tests.LookTests.Pages_and_css_reference_no_external_hosts(path: "/")
  Failed Ballpass.Web.Tests.LookTests.Font_faces_point_to_shipped_woff2_files_with_licenses
  Failed Ballpass.Web.Tests.LookTests.Dark_text_on_accent_meets_4_5
  Failed Ballpass.Web.Tests.LookTests.Home_trust_strip_uses_icon_badges_for_all_three_sources
Failed!  - Failed:    18, Passed:     0, Skipped:     0, Total:    18, Duration: 672 ms - Ballpass.Web.Tests.dll (net10.0)
```
Reasons: no data-theme/data-bs-theme on <html>; bp-tokens.css / bp-base.css / wwwroot/css missing (DirectoryNotFound on the contrast,
font and external-host tests); no data-source badge markup with svg.

## Green run (task 4) — 2026-11-21
```
BALLPASS_TESTDB_SUFFIX=_look dotnet test
Passed!  - Failed: 0, Passed: 73, Skipped: 0, Total: 73   (55 existing + 18 LookTests cases), 0 warnings
```
Computed contrast (ratio on bg / s1 / s2): text 16.45/15.26/13.55, muted 7.96/7.38/6.55, accent 6.73/6.24/5.54,
success 10.87/10.08/8.95, src-self 9.14/8.48/7.53, src-coach 9.59/8.90/7.90, src-league 7.85/7.28/6.47,
border-ui 3.71/3.44/3.05, accent-contrast on accent 6.73. (warn 10.51/9.75/8.65 and danger 6.41/5.94/5.27 for information.)

## Visual check (task 5) — Brave headless, app on :5280 (5273 was in use), scrollWidth / innerWidth
```
1280: home 1280/1280  profile 1280/1280  demo 1280/1280  search(coach) 1280/1280  shortlist(coach) 1280/1280
 390: home 390/390    profile 390/390    demo 390/390    search(coach) 390/390    shortlist(coach) 390/390   inbox(athlete) 390/390
```
Screenshots: scratchpad look-{home,profile,demo,search,shortlist}-{d,m}.png, look-{inbox,denied,404}-d.png, look-inbox-m.png,
look-profile-league-en.png, look-greek-caps.png (Greek capitals with tonos in both fonts; both @font-face report "loaded").
Fixed after looking: (1) source badge text clipped in the stats table (child component gets no scope attribute, so the wrap rule
moved to global app.css); (2) persona cards on /demo had uneven heights (form now flex); (3) key-stat numbers spread across the
card, now a compact flex row. Earlier catch before screenshots: .bp-cta secondary rule overrode .bp-cta-primary (accent fill), fixed by selector.
