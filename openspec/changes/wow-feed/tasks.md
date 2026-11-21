# Tasks: wow-feed

- [x] 1. Failing tests (tests/Ballpass.Web.Tests/FeedTests.cs, 53 tests incl. theories). **Red run recorded below.**
- [x] 2. Entities + migration `WowFeed` (Data/FeedEntities.cs, one `ApplyConfigurationsFromAssembly` line).
- [x] 3. FeedSeeder (40 posts, 45 kudos, follows), one call in DemoDataSeeder.
- [x] 4. FeedService / FollowService, endpoints (`AddFeed` / `MapFeed`), shortlist seam (`TryFollowAsync`).
- [x] 5. Components (PostCard, CourtArt, Composer, FeedNavLink, FeedPreview, FollowButton), page `/feed`, bp-feed.css, FeedResource (el + en).
- [x] 6. Green: `BALLPASS_TESTDB_SUFFIX=_feed dotnet test`: 126 passed, 0 failed (73 existing + 53 feed).
- [x] 7. Screenshots (scratchpad): feed-coach-1280.png, feed-coach-full.png, feed-coach-390.png, feed-home-390.png. Read; fixed: nav link colour (layout scoped CSS does not reach child anchors), duplicate composer hint.

## Red run (task 1)
Stubs returned empty results (no compile errors). `dotnet test --filter FullyQualifiedName~Feed`:
`Failed! - Failed: 44, Passed: 9, Total: 53` (the 9 passing were pure negatives or the IsAdult theory).
Typical assertion failures: `Seed_has_40_posts...: Expected 40, Actual 0`; `Anonymous_post_is_403: Expected Forbidden, Actual MethodNotAllowed`;
`Show_more_pages...: Assert.InRange Actual 0`; `Service_rejects_an_author_under_18...: Expected Minor, Actual Invalid`.

## Green run (task 6)
`Passed! - Failed: 0, Passed: 126, Skipped: 0, Total: 126`

## Caveats
- Newly posted items rank by SortAt (boosted looking-for-team/tryout seed posts can sit above a fresh post); page one still contains it.
- No admin `/admin/reports`, no saved-search items, no "a coach noticed you" inbox line (out of this package's scope).
- Club posts are seed-only (via head coach).
