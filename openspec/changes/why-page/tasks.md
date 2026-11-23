# Tasks — why-page

- [x] 1. Research: Notion "Project B" problem wording (no numbers) + web sources → `docs/research/problem.md`.
- [x] 2. Red tests `tests/Ballpass.Web.Tests/WhyTests.cs` (record the red run below).
- [x] 3. `WhyResource` el + en, `bp-why.css` (+1 link in App.razor), `Components/Why/*`, page `/why`.
- [x] 4. Home band (1 line in Home.razor), footer + signed-out nav link (2 lines in MainLayout.razor).
- [x] 5. Green: changed tests, then full suite once (`BALLPASS_TESTDB_SUFFIX=_why dotnet test --blame-hang-timeout 5m`), 0 warnings.
- [x] 6. Screenshots 1512 + 390 of `/why`, home 1512 viewport + full; read and fix.

## Red run
`BALLPASS_TESTDB_SUFFIX=_why dotnet test --filter "FullyQualifiedName~WhyTests"` (2026-11-23, before any page/component; only the
empty WhyResource marker + resx existed so the file compiled):
```
  Failed WhyTests.Why_renders_all_six_sections_in_order(culture: "en", ...)   -> Expected: OK  Actual: NotFound
  Failed WhyTests.Why_renders_all_six_sections_in_order(culture: null, ...)
  Failed WhyTests.Why_has_no_demo_labels(culture: null) / (culture: "en")
  Failed WhyTests.Why_resource_uses_the_formal_register_and_has_both_languages
  Failed WhyTests.Why_doors_link_to_the_demo_page_when_the_demo_is_disabled
  Failed WhyTests.Today_shows_both_sides_and_the_fragments
  Failed WhyTests.Signed_out_nav_and_footer_link_to_why
  Failed WhyTests.Why_doors_post_the_demo_login
  Failed WhyTests.Proof_counters_equal_the_database_counts
  Failed WhyTests.Every_figure_in_today_and_cost_has_a_source_link(culture: "en") / (culture: null)
  Failed WhyTests.Signed_in_role_nav_has_no_why_link_but_the_footer_does
  Failed WhyTests.Home_has_the_problem_band_after_the_doors_with_a_link_to_why
  Failed WhyTests.How_has_four_coloured_steps_each_with_a_preview
Failed!  - Failed:    15, Passed:     0, Skipped:     0, Total:    15
```
Two test fixes during green (test bugs, not behaviour): `<main>` carries a scoped-CSS attribute (regex now `<main\b[^>]*>`);
decorative `<svg>` (jersey numbers) is stripped before the "no unsourced digit" check.

## Green run
- `WhyTests`: `Passed!  - Failed: 0, Passed: 15, Total: 15`
- Full suite once: `BALLPASS_TESTDB_SUFFIX=_why dotnet test --blame-hang-timeout 5m` → `Passed!  - Failed: 0, Passed: 632, Skipped: 0, Total: 632`
- `dotnet build`: 0 Warning(s), 0 Error(s)

## Screenshots
App on :5305 (DB ballpass_why_dev, reset first), Brave headless via playwright-cli; files in scratchpad/why/ (read):
why-1512-full, why-1512-view, why-390-full, home-1512-view, home-1512-full, home-390-full. scrollWidth = innerWidth at 1512 and 390.
Home 1512: doors end at y=888 < 945 viewport (above the fold); band starts at y=936.
Fixed after reading: proof strip was wider than the content (background moved to an inner box); step previews equal height;
the threads' ✕ was hidden under the PDF fragment (moved to the top gap); phone steps got a vertical connector; door photos are
lazy, so the script scrolls before the full-page shot.
