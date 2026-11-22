# Tasks: wow-engage

- [x] 1. Failing tests first (tests/Ballpass.Web.Tests/EngageTests.cs, 64 tests incl. theories). Red run below.
- [x] 2. Entities Comment / CommentReport / PostTag (Data/EngageEntities.cs) + migration `WowEngage`.
- [x] 3. PostTagParser, EngageService, EngageCache, endpoints `/feed/comment[/delete|/hide|/report]` (AddEngage / MapEngage).
- [x] 4. Seams: PostCard (id anchor, EngageBar, EngageComments, kudos/follow return to #post-N), FeedPage (tag filter, below-tabs, inline, sidebar, preload), FeedService (tag filter + tag indexing on create), FeedSetup (keep #fragment), App.razor (css), Program.cs (2 lines), DemoDataSeeder (1 line).
- [x] 5. EngageSeeder: 150 comments, kudos 45 -> 250, hashtags on 16 of 40 posts.
- [x] 6. Green: `BALLPASS_TESTDB_SUFFIX=_engage dotnet test`: 495 passed, 0 failed (431 existing + 64 engage).
- [x] 7. Screenshots (scratchpad engage-*.png) read; fixed: likers line moved into the counts row, who-to-follow button under the name (names broke mid-word), comment hint made screen-reader-only (a visible hint x10 broke an existing "hint once" test).

## Red run (task 1)
Stubs returned empty results (no compile errors). `dotnet test --filter FullyQualifiedName~Engage`:
`Failed! - Failed: 48, Passed: 16, Total: 64` (the 16 passing were pure negatives: invalid-tag parsing, absent pill, throwing-service pages).
Typical assertion failures: `Anonymous_comment_is_403: Expected Forbidden, Actual MethodNotAllowed`; `Seed_has_150_comments: Expected 150, Actual 0`;
`Parser_takes_at_most_3_tags: Expected [aa, bb, cc], Actual []`; `Unknown_tag_shows_an_empty_state: data-feed-empty not found`.

## Green run (task 6)
`Passed! - Failed: 0, Passed: 495, Skipped: 0, Total: 495`

## Reversal
`FeedTests.Only_the_author_sees_the_kudos_count` was changed to `Kudos_count_is_public_...` (count visible to all). `IFeedService.GetFeedAsync` got an optional `tag` parameter (the throwing test fake was updated).

## Caveats
- Test posts of EngageTests are 40 days old and found via their own hashtag, so they never crowd the shared feed's 60 places.
- Seeded tags of tryout / stat / badge / highlight posts live in the index only (those cards have no free text); LFT and free-text posts get the hashtag appended to their text.
- Trends are computed per request from the tag index (no cache). The new-posts pill links to /feed (cookie reset), not an anchor.
- Migration `WowEngage` is for local testing; regenerate at merge (MEDIA also adds one).
