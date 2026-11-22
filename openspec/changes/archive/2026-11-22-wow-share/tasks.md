# Tasks — wow-share

- [x] 1. Failing `ShareTests` (tests/Ballpass.Web.Tests/ShareTests.cs) with minimal stubs. **Record red run.**
- [x] 2. Fonts + packages, Skia renderer, cache service, endpoint; early real render on M1 (macOS arm64 OK).
- [x] 3. ShareMeta, SharePanel, Season page, ShareResource, bp-share.css.
- [x] 4. Green: `BALLPASS_TESTDB_SUFFIX=_share dotnet test`. **Record green run.**
- [x] 5. Screenshots 1280x900 + 390x844 (profile share panel, wrapped slides, og.png), read, fix (see below).
- [ ] 6. (not done, scope) Player card 1080x1350: only if time remains after 1 and 2 per DECISIONS.md.

## Red run (task 1) — 2026-11-21
`BALLPASS_TESTDB_SUFFIX=_share dotnet test --filter FullyQualifiedName~ShareTests` against stubs (renderer returned `[]`, no routes, genitive returned its input, cohort returned null)
```
Failed: 16, Passed: 2, Total: 18
  Greek_female_first_name_genitive x4            Expected "Ελένης" / Actual "Ελένη"
  Greek_genitive_gives_null_when_no_safe_rule    Expected null / Actual "Μυρτώ"
  Og_png_is_1200x630_png_under_300KB_without_cookie   Expected OK / Actual NotFound
  Og_image_uses_PublicBaseUrl_when_configured    Sub-string not found
  Profile_has_absolute_og_image_tags_...         Pattern not found
  Profile_share_panel_has_preview_download_...   Pattern not found
  Cohort_below_20_..._highest_midrank_metric     Assert.NotNull() Failure: Value is null
  Second_request_..._304, Watermark_bands_..., Long_greek_name_..., Wrapped_* (route missing / empty PNG)
(Passed 2 = Unknown_slug_is_404 and Cohort_uses_only_the_same_position_group, trivially true against stubs)
```

## Green run (task 4) — 2026-11-21
`BALLPASS_TESTDB_SUFFIX=_share dotnet test` (whole suite)
```
Passed!  - Failed: 0, Passed: 91, Skipped: 0, Total: 91 - Ballpass.Web.Tests.dll (net10.0)
```
First full run had 1 failure: existing `ProfileTests.Open_graph_title_contains_full_name`. Cause: a second `<HeadContent>` on a page
REPLACES the first (it does not append), so `ShareMeta` had wiped og:title. Fix: ShareMeta renders bare `<meta>` tags and is
placed INSIDE the page's single HeadContent; regression assertion added to ShareTests.

## Measured (M1 Pro, Development build, localhost; not estimates)
| What | Result |
|---|---|
| First render ever after a cold build | 480 ms (includes native Skia + font load, cold OS cache) |
| First render in a freshly started process (warm OS cache) | 63 ms |
| Warm renders, 299 athletes | avg 24 ms, max 63 ms |
| Cached 200 (memory) / 304 | 2.7 ms / 2.8 ms (curl total) |
| PNG size, 300 athletes | max 158,323 bytes (limit 300 KB) |

## Screenshots (scratchpad)
share-og-*.png, share-profile-1280.png, share-wrapped-d{0..5}.png (1280), share-wrapped-m{0..5}.png (390, scrollWidth == viewport).
Fixed after reading: wordmark crowding the name (moved), "(demo)" printed twice for demo club names (de-duplicated),
"2025-/26" breaking at the hyphen in the intro (nowrap span), growth numbers overflowing the slide (smaller size).
